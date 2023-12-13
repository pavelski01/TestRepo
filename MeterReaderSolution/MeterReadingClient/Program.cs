using MeterReadingClient;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddTransient<ReadingGenerator>();
        services.AddHostedService<Worker>();
    })
    .Build();

host.Run();
