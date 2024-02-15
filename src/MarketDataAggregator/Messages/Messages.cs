using Akka.Actor;

namespace MarketDataAggregator.Messages;

public sealed record FetchKlineSnapshot;
public sealed record SubscribeToKlineUpdates;
public sealed record Unsubscribe(int SubscriptionId);
public sealed record KlineUpdateReceived(string Symbol, DateTime OpenTime, decimal ClosePrice, IActorRef ConsoleWriterRef);
public sealed record KlineUpdateConsumed(string Symbol, DateTime OpenTime, decimal ClosePrice);

public sealed class GracefulShutdown
{
    private GracefulShutdown()
    {
    }

    /// <summary>
    /// The singleton instance of <see cref="GracefulShutdown"/>.
    /// </summary>
    public static GracefulShutdown Instance { get; } = new();
}
