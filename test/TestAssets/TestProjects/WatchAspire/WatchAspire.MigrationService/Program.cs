using MigrationService;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
