namespace MarketDataAggregator.Messages;

public interface IMarketDataEvent;

public sealed record KlineUpdateReceived(string Symbol, DateTime OpenTime, decimal ClosePrice) : IMarketDataEvent;
