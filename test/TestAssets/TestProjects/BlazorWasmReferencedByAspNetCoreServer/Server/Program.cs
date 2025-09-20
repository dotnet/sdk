var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapStaticAssets();
app.MapFallbackToFile("index.html");

app.Run();
