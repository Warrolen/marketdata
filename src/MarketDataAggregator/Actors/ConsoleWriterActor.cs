using Akka.Actor;

namespace MarketDataAggregator.Actors;

/// <summary>
/// Actor responsible for serializing message writes to the console.
/// (write one message at a time, champ :)
/// </summary>
public class ConsoleWriterActor : UntypedActor
{
    protected override void OnReceive(object message)
    {
        if (message is string)
        {
            Console.WriteLine($"[CONSOLE WRITER]: {message}");
        }
    }
}
