using Akka.Actor;
using Binance.Net.Enums;
using MarketDataAggregator.Actors;

var actorSystem = ActorSystem.Create("MarketData");

var consoleWriterActor = actorSystem.ActorOf(Props.Create<ConsoleWriterActor>(), "consoleWriter");

var props = Props.Create(() => new MarketDataSubscriberActor("BTCUSDT", KlineInterval.OneMinute, consoleWriterActor));
var marketDataSubscriberActor = actorSystem.ActorOf(props, "marketDataSubscriber");

Console.WriteLine($"Press any key to stop the {nameof(MarketDataSubscriberActor)}...");
Console.ReadKey();

// TODO: Fix dead letter message
marketDataSubscriberActor.Tell(PoisonPill.Instance);

Console.ReadKey();
