// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Watch.BrowserRefresh;

[TestClass]
public class HostingStartupTest
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ConfigureServices_RegistersBrowserToolsServices()
    {
        var services = new ServiceCollection();

        new HostingStartup().ConfigureServices(services, new Uri("http://127.0.0.1:5000"));

        var tagHelperDescriptors = services
            .Where(static descriptor => descriptor.ServiceType == typeof(ITagHelperComponent))
            .ToArray();
        Assert.HasCount(1, tagHelperDescriptors);
        Assert.AreEqual(typeof(BrowserRefreshTagHelperComponent), tagHelperDescriptors[0].ImplementationType);
        Assert.Contains(
            static descriptor => descriptor.ServiceType == typeof(BrowserToolsForwarder),
            services);
    }

    /// <summary>
    /// The .NET 9 WebAssembly runtime fetches the legacy replay endpoint from the application origin
    /// when its Hot Reload agent starts. The endpoint no longer exists, so without a local answer the
    /// SPA fallback returns the application's HTML and the runtime reports a JSON parse failure. The
    /// application answers it with an empty update set: replay happens over the authenticated
    /// WebSocket, so nothing may be served over this unauthenticated route.
    /// </summary>
    [TestMethod]
    public async Task LegacyPreviousDeltas_IsAnsweredLocallyWithAnEmptyUpdateSet()
    {
        await using var application = await StartApplicationAsync();
        using var client = new HttpClient { BaseAddress = GetAddress(application) };

        using var response = await client.GetAsync(ApplicationPaths.LegacyPreviousDeltas, TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.AreEqual("[]", await response.Content.ReadAsStringAsync(TestContext.CancellationToken));
    }

    /// <summary>
    /// Only the runtime's GET probe is answered. Anything else keeps reaching the application, which
    /// is what stops the route from becoming an update surface of its own.
    /// </summary>
    [TestMethod]
    public async Task LegacyPreviousDeltas_DoesNotHandleNonGetRequests()
    {
        await using var application = await StartApplicationAsync();
        using var client = new HttpClient { BaseAddress = GetAddress(application) };

        using var response = await client.PostAsync(
            ApplicationPaths.LegacyPreviousDeltas,
            content: null,
            TestContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.AreEqual("application", await response.Content.ReadAsStringAsync(TestContext.CancellationToken));
    }

    private static async Task<WebApplication> StartApplicationAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        new HostingStartup().ConfigureServices(builder.Services, new Uri("http://127.0.0.1:5000"));

        var application = builder.Build();

        // Stands in for the application's own terminal middleware, which in a real Blazor WebAssembly
        // host is the SPA fallback that would otherwise answer the runtime's probe with HTML.
        application.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return context.Response.WriteAsync("application", context.RequestAborted);
        });

        await application.StartAsync();
        return application;
    }

    private static Uri GetAddress(WebApplication application)
    {
        var addresses = application.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()!
            .Addresses;

        return new Uri(addresses.Single());
    }
}
