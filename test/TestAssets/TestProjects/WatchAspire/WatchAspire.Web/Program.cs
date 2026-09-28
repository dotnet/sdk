using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Hosting;
using WatchAspire.Web.Components;

var builder = WebApplication.CreateBuilder(args);

/* top-level placeholder */

var app = builder.Build();
app.MapGet("/", () => "Hello world!");
app.Run();