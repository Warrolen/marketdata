namespace MarketDataAggregator.Messages;

public interface IMarketDataCommand;

public sealed record FetchKlineSnapshot : IMarketDataCommand;
public sealed record SubscribeToKlineUpdates : IMarketDataCommand;
public sealed record UnsubscribeFromKlineUpdates(int SubscriptionId) : IMarketDataCommand;
