using Akka.Actor;

namespace MarketData.Actors;

public class TickerActor : ReceiveActor
{
    public TickerActor(string exchange, string symbol)
    {
        Receive<decimal>(price =>
        {
            Console.WriteLine($"{Context.Self.Path} - {exchange}:{symbol} - {price}");
        });
    }
}
