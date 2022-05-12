using SensorWorker;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
        services.AddApplicationMonitoring();
    })
    .Build();

await host.RunAsync();
