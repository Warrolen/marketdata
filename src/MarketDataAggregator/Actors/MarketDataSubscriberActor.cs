using Akka.Actor;
using Binance.Net.Clients;
using Binance.Net.Enums;
using MarketDataAggregator.Messages;
using Petabridge.Collections;

namespace MarketDataAggregator.Actors;

// TODO: Add a query for getting kline history.
/// <summary>
/// This actor keeps an in-memory snapshot of the kline data for fast lookup, so we don't have to initiate network I/O calls to Binance everytime.
/// It will later on be wired up with MarketDataAggregatorActor, which is going calculate exponential moving average, or something like that.
/// </summary>
public class MarketDataSubscriberActor : ReceiveActor, IWithUnboundedStash
{
    private const int DefaultSampleSize = 20;

    private readonly string _coin;
    private readonly KlineInterval _interval;
    private readonly IActorRef _consoleWriterActor;
    private readonly BinanceRestClient _restClient;
    private readonly BinanceSocketClient _socketClient;
    private readonly CircularBuffer<KlineUpdateReceived> _klineUpdates;

    public MarketDataSubscriberActor(string coin, KlineInterval interval, IActorRef consoleWriterActor)
    {
        _coin = coin;
        _interval = interval;
        _consoleWriterActor = consoleWriterActor;

        _restClient = new BinanceRestClient();
        _socketClient = new BinanceSocketClient();
        _klineUpdates = new CircularBuffer<KlineUpdateReceived>(DefaultSampleSize);

        WaitingForRestApiCall();
    }

    private void WaitingForRestApiCall()
    {
        ReceiveAsync<FetchKlineSnapshot>(async _ => await FetchKlineSnapshot());
        Receive<SubscribeToKlineUpdates>(_ => Stash.Stash());
    }

    private async Task FetchKlineSnapshot()
    {
        var callResult = await _restClient.SpotApi.ExchangeData.GetKlinesAsync(_coin, _interval, limit: DefaultSampleSize);
        if (!callResult.Success)
        {
            // TODO: Handle System.ArgumentException: 0 not allowed for parameter limit, min: 1, max: 1500 (Parameter 'limit')
            _consoleWriterActor.Tell($"Error while fetching kline data: {callResult.Error?.Message} - retrying in 5s.");
            Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(5), Self, new FetchKlineSnapshot(), ActorRefs.NoSender);
            return;
        }

        foreach (var kline in callResult.Data)
        {
            _klineUpdates.Enqueue(new KlineUpdateReceived(_coin, kline.OpenTime, kline.ClosePrice));
        }

        _consoleWriterActor.Tell($"Pre-filled {DefaultSampleSize} kline data for {_coin}.");

        BecomeReadyForSubscription();
    }

    private void BecomeReadyForSubscription()
    {
        Become(ReadyForSubscription);
        Stash.UnstashAll(); // Process any messages that arrived while fetching initial data
    }

    private void ReadyForSubscription()
    {
        ReceiveAsync<SubscribeToKlineUpdates>(_ => SubscribeToKlineUpdates());
        Receive<KlineUpdateReceived>(msg => HandlePriceUpdate(msg));
        Receive<UnsubscribeFromKlineUpdates>(_ => Stash.Stash());
    }

    private async Task SubscribeToKlineUpdates()
    {
        var subscriptionResult = await _socketClient.SpotApi.ExchangeData.SubscribeToKlineUpdatesAsync(_coin, _interval, data =>
        {
            if (data.Data.Data.Final)
            {
                Self.Tell(new KlineUpdateReceived(data.Data.Symbol, data.Data.Data.OpenTime, data.Data.Data.ClosePrice));
            }
        });

        if (!subscriptionResult.Success)
        {
            _consoleWriterActor.Tell($"Error while trying to subscribe to kline updates: {subscriptionResult.Error?.Message} - retrying in 5s.");
            Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(5), Self, new SubscribeToKlineUpdates(), ActorRefs.NoSender);
            return;
        }

        _consoleWriterActor.Tell("Subscription to kline updates successful.");

        // Immediately stash an unsubscribe message for the cleanup phase, so we don't have to keep a subscriptionResult class reference.
        Self.Tell(new UnsubscribeFromKlineUpdates(subscriptionResult.Data.Id));
    }

    private void HandlePriceUpdate(KlineUpdateReceived msg)
    {
        _klineUpdates.Enqueue(new KlineUpdateReceived(msg.Symbol, msg.OpenTime, msg.ClosePrice));
        _consoleWriterActor.Tell($"Real-time update for {msg.Symbol}: Time: {msg.OpenTime}, Close Price: {msg.ClosePrice}");
    }

    private void Cleanup()
    {
        ReceiveAsync<UnsubscribeFromKlineUpdates>(async msg =>
        {
            await _socketClient.UnsubscribeAsync(msg.SubscriptionId);
            _consoleWriterActor.Tell("Unsubscribed from kline updates successfully.");
        });
    }

    protected override void PreStart()
    {
        Self.Tell(new FetchKlineSnapshot());
        Self.Tell(new SubscribeToKlineUpdates());
    }

    protected override void PostStop()
    {
        Become(Cleanup);
        Stash.Unstash(); // This will process the UnsubscribeFromKlineUpdates message
    }

    public IStash Stash { get; set; } = null!; // TODO: Is there some better way to handle the nullability warning here?
}
