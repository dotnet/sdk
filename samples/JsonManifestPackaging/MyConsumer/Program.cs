var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseStaticFiles();
app.MapStaticAssets();

app.MapGet("/", () => "Hello from MyConsumer - JSON Manifest Packaging Research");

app.Run();
