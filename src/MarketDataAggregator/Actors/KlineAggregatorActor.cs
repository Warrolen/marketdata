using Akka.Actor;
using MarketDataAggregator.Messages;

namespace MarketDataAggregator.Actors;

// TODO: Wire this up with the KlineSubscriberActor

/// <summary>
/// This actor consumes <see cref="KlineUpdateReceived"/> events and aggregates the ClosePrice to produce the Relative Strength Index (RSI).
///
/// It transparently tackles the concern: "will the data processing ever catch up with the incoming stream data?".
/// If not, then we're facing a thundering herd problem. No matter how we process it, if we're processing sequentially, we never win.
/// </summary>
public class KlineAggregatorActor : ReceiveActor
{
    public KlineAggregatorActor()
    {
        Receive<KlineUpdateReceived>(HandleKlineUpdateReceived);
    }

    private void HandleKlineUpdateReceived(KlineUpdateReceived msg)
    {
        msg.ConsoleWriterRef.Tell($"Processed update for {msg.Symbol}: Time: {msg.OpenTime}, Close Price: {msg.ClosePrice}");

        Context.Parent.Tell(new KlineUpdateConsumed(msg.Symbol, msg.OpenTime, msg.ClosePrice));

        // After processing, the actor can stop itself or be reused based on your design.
        Context.Stop(Self);
    }
}
