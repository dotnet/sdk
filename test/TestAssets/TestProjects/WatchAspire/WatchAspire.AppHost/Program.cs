var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.WatchAspire_ApiService>("apiservice");

builder.AddProject<Projects.WatchAspire_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
