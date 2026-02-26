var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapStaticAssets();
app.MapGet("/", () => "Hello World!");
app.Run();
