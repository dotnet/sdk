using MigrationService;
using Microsoft.Extensions.Hosting;
using System.IO;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

var dir = Environment.GetEnvironmentVariable("HELIX_WORKITEM_UPLOAD_ROOT") is { } ciOutputRoot
    ? Path.Combine(ciOutputRoot, ".hotreload", "AspireMigration")
    : "AspireMigration.hotreload";

Directory.CreateDirectory(dir);

var d = new List<string>();
foreach (DictionaryEntry e in Environment.GetEnvironmentVariables())
{
    d.Add($"{e.Key}={e.Value}");
}

File.WriteAllLines(Path.Combine(dir, "env.txt"), d);

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
