using Akka.Actor;
using Binance.Net.Clients;
using Binance.Net.Enums;
using MarketDataAggregator.Messages;
using Petabridge.Collections;

namespace MarketDataAggregator.Actors;

public class KlineSubscriberActor : ReceiveActor, IWithUnboundedStash
{
    private const int DefaultSampleSize = 20;

    private readonly string _coin;
    private readonly KlineInterval _interval;
    private readonly IActorRef _consoleWriterActor;
    private readonly BinanceRestClient _restClient;
    private readonly BinanceSocketClient _socketClient;
    private readonly CircularBuffer<KlineUpdateReceived> _klineUpdates;

    public KlineSubscriberActor(string coin, KlineInterval interval, IActorRef consoleWriterActor)
    {
        _coin = coin;
        _interval = interval;
        _consoleWriterActor = consoleWriterActor;
        _restClient = new BinanceRestClient();
        _socketClient = new BinanceSocketClient();
        _klineUpdates = new CircularBuffer<KlineUpdateReceived>(DefaultSampleSize);
        Become(Started);
    }

    private void Started()
    {
        ReceiveAsync<FetchKlineSnapshot>(HandleFetchKlineSnapshot);
        Receive<SubscribeToKlineUpdates>(StashSubscribeToKlineUpdates);
    }

    private void Initialized()
    {
        ReceiveAsync<SubscribeToKlineUpdates>(HandleSubscribeToKlineUpdates);
        Receive<KlineUpdateReceived>(HandleKlineUpdateReceived);
        Receive<Unsubscribe>(StashUnsubscribe);
        Receive<GracefulShutdown>(HandleGracefulShutdown);
    }

    private void Stopping()
    {
        ReceiveAsync<Unsubscribe>(HandleUnsubscribe);
    }

    protected override void PreStart()
    {
        // TODO: TEST PURPOSES ONLY
        _consoleWriterActor.Tell("The actor will shutdown in 15 seconds for testing purposes");
        Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(15), Self, GracefulShutdown.Instance, Self);

        Self.Tell(new FetchKlineSnapshot());
        Self.Tell(new SubscribeToKlineUpdates());
    }

    private async Task HandleFetchKlineSnapshot(FetchKlineSnapshot msg)
    {
        var callResult = await _restClient.SpotApi.ExchangeData.GetKlinesAsync(_coin, _interval, limit: DefaultSampleSize);
        if (!callResult.Success)
        {
            _consoleWriterActor.Tell($"Error while fetching kline data: {callResult.Error?.Message} - retrying in 5s.");
            Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(5), Self, new FetchKlineSnapshot(), ActorRefs.NoSender);
            return;
        }

        foreach (var kline in callResult.Data)
        {
            _klineUpdates.Enqueue(new KlineUpdateReceived(_coin, kline.OpenTime, kline.ClosePrice, _consoleWriterActor));
        }

        _consoleWriterActor.Tell($"Pre-filled {DefaultSampleSize} kline data for {_coin}.");

        Become(Initialized);
        Stash.UnstashAll(); // Process any SubscribeToKlineUpdates messages that arrived while fetching initial data.
    }

    private void StashSubscribeToKlineUpdates(SubscribeToKlineUpdates msg)
    {
        Stash.Stash();
    }

    private async Task HandleSubscribeToKlineUpdates(SubscribeToKlineUpdates msg)
    {
        // Use closure over Self to deal with the side effect of having to access static variables reference value inside an async function.
        var self = Self;

        var subscriptionResult = await _socketClient.SpotApi.ExchangeData.SubscribeToKlineUpdatesAsync(_coin, _interval, data =>
        {
            if (data.Data.Data.Final)
            {
                self.Tell(new KlineUpdateReceived(data.Data.Symbol, data.Data.Data.OpenTime, data.Data.Data.ClosePrice, _consoleWriterActor));
            }
        });

        if (!subscriptionResult.Success)
        {
            _consoleWriterActor.Tell($"Error while trying to subscribe to kline updates: {subscriptionResult.Error?.Message} - retrying in 5s.");
            Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(5), Self, new SubscribeToKlineUpdates(), ActorRefs.NoSender);
            return;
        }

        _consoleWriterActor.Tell("Subscription to kline updates successful.");
        Self.Tell(new Unsubscribe(subscriptionResult.Data.Id));
    }

    private void HandleKlineUpdateReceived(KlineUpdateReceived msg)
    {
        _klineUpdates.Enqueue(msg with { ConsoleWriterRef = _consoleWriterActor });
        _consoleWriterActor.Tell($"Real-time update for {msg.Symbol}: Time: {msg.OpenTime}, Close Price: {msg.ClosePrice}");

        // TODO: KlineAggregatorActor here.
        // TODO: ReconcilierActor here to make sure the data is consistent in case of a crash.
        // TODO: Actor to warm-up the RSI indicator.
    }

    private async Task HandleUnsubscribe(Unsubscribe msg)
    {
        await _socketClient.UnsubscribeAsync(msg.SubscriptionId);
        _consoleWriterActor.Tell("Unsubscribed from the kline data stream.");

        // After unsubscribing, the actor can stop itself.
        Context.Stop(Self);
    }

    private void StashUnsubscribe(Unsubscribe msg)
    {
        Stash.Stash();
    }

    private void HandleGracefulShutdown(GracefulShutdown obj)
    {
        Become(Stopping);
        Stash.UnstashAll();
    }

    public IStash Stash { get; set; } = default!;
}
