var builder = DistributedApplication.CreateBuilder(args);

var migration = builder.AddProject<Projects.WatchAspire_MigrationService>("migrationservice");

var apiService = builder
    .AddProject<Projects.WatchAspire_ApiService>("apiservice")
    .WaitForCompletion(migration);

builder.AddProject<Projects.WatchAspire_Web>("webfrontend")
    .WaitForCompletion(migration)
    .WithExternalHttpEndpoints()
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
