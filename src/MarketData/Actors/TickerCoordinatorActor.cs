using Akka.Actor;
using MarketData.Messages;

namespace MarketData.Actors;

/// <summary>
/// Child-per entity parent for tickers.
/// </summary>
public class TickerCoordinatorActor : ReceiveActor
{
    public TickerCoordinatorActor()
    {
        Receive<TickerUpdate>(ticker =>
        {
            var (exchange, symbol, price) = ticker;
            var childName = $"{exchange}-{symbol}";
            var tickerActor = Context.Child(childName).GetOrElse(() => StartChild(exchange, symbol, childName));
            tickerActor.Forward(price);
        });
    }

    private static IActorRef StartChild(string exchange, string symbol, string childName) =>
        Context.ActorOf(Props.Create(() => new TickerActor(exchange, symbol)), childName);
}
