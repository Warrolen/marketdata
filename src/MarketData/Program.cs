using Akka.Actor;
using MarketData.Actors;
using MarketData.Messages;

var actorSystem = ActorSystem.Create("MarketData");

var props = Props.Create<TickerDispatcherActor>();
var dispatcher = actorSystem.ActorOf(props, "TickerDispatcherActor");

dispatcher.Tell(new TickerUpdate("Binance", "ABC", 1.20m));
dispatcher.Tell(new TickerUpdate("Binance", "XYZ", 0.59m));
dispatcher.Tell(new TickerUpdate("Kraken", "ABC", 1.21m));
dispatcher.Tell(new TickerUpdate("Kraken", "HBZ", 0.86m));
dispatcher.Tell(new TickerUpdate("Huobi", "FUK", 1.20m));
dispatcher.Tell(new TickerUpdate("Huobi", "XYZ", 0.57m));

// blocks the main thread from exiting until the actor system is shut down
actorSystem.WhenTerminated.Wait();
