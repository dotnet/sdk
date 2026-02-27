// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseStaticFiles();
app.MapStaticAssets();

app.MapGet("/", () => "Hello from MyConsumer - JSON Manifest Packaging Research");

app.Run();
