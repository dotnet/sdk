var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.WatchAspire_ApiService>("apiservice");

builder.Build().Run();
