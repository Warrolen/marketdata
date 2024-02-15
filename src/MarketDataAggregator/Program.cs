using Akka.Actor;
using Akka.Hosting;
using Binance.Net.Enums;
using MarketDataAggregator.Actors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

var builder = new HostBuilder()
    .ConfigureAppConfiguration(c => c.AddEnvironmentVariables()
        .AddJsonFile("appsettings.json")
        .AddJsonFile($"appsettings.{environment}.json"))
    .ConfigureServices((builderContext, services) =>
    {
        services.AddAkka("Rsi", (configurationBuilder, provider) =>
        {
            configurationBuilder.WithActors((system, registry) =>
            {
                var consoleWriterProps = Props.Create(() => new ConsoleWriterActor());
                var consoleWriterActor = system.ActorOf(consoleWriterProps, "consoleWriterActor");

                var subscriberProps = Props.Create(() => new KlineSubscriberActor("BTCUSDT", KlineInterval.OneMinute, consoleWriterActor));
                var subscriberActor = system.ActorOf(subscriberProps, "subscriberActor");

                registry.Register<ConsoleWriterActor>(consoleWriterActor);
                registry.Register<KlineSubscriberActor>(subscriberActor);
            });
        });
    })
    .Build();

await builder.RunAsync();
