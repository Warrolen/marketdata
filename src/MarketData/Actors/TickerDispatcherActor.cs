using Akka.Actor;
using MarketData.Messages;

namespace MarketData.Actors;

public class TickerDispatcherActor : ReceiveActor
{
    public TickerDispatcherActor()
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
