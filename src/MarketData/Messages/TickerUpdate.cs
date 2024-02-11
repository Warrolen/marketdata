namespace MarketData.Messages;

public sealed record TickerUpdate(string Exchange, string Symbol, decimal Price);
