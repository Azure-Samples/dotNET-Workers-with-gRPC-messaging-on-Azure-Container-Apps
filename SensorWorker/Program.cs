using SensorWorker;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
        services.AddWorkerApplicationMonitoring();
    })
    .Build();

await host.RunAsync();
