// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

[assembly: HostingStartup(typeof(Microsoft.AspNetCore.Watch.BrowserRefresh.HostingStartup))]

namespace Microsoft.AspNetCore.Watch.BrowserRefresh;

internal sealed class HostingStartup : IHostingStartup, IStartupFilter
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services => ConfigureServices(services, BrowserToolsEnvironment.GetProviderAddress()));
    }

    internal void ConfigureServices(IServiceCollection services, Uri providerAddress)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupFilter>(this));
        services.TryAddSingleton(services => new BrowserToolsForwarder(providerAddress, services.GetRequiredService<ILogger<BrowserToolsForwarder>>()));
        services.AddHttpContextAccessor();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITagHelperComponent, BrowserRefreshTagHelperComponent>());
    }

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            // Only the endpoints the provider actually exposes are forwarded. The route is registered
            // ahead of UsePathBase so it is absolute, which matches the route pinned into the
            // generated configuration module.
            app.MapWhen(
                static context =>
                    context.Request.Path.Equals(ApplicationPaths.BrowserToolsConnect, StringComparison.OrdinalIgnoreCase) ||
                    context.Request.Path.Equals(ApplicationPaths.BrowserToolsClearCache, StringComparison.OrdinalIgnoreCase),
                static browserTools =>
                {
                    browserTools.UseWebSockets();
                    browserTools.Run(
                        context => context.RequestServices.GetRequiredService<BrowserToolsForwarder>().ForwardAsync(context));
                });

            // The .NET 9 WebAssembly runtime probes the legacy HTTP replay endpoint when its Hot Reload
            // agent starts and reports an error unless it receives a successful JSON response. Updates
            // produced before a browser connected are replayed over the authenticated WebSocket, so the
            // application answers the probe locally with an empty update set. Nothing is forwarded to the
            // provider and no update is ever served over this unauthenticated route.
            app.MapWhen(
                static context =>
                    HttpMethods.IsGet(context.Request.Method) &&
                    context.Request.Path.Equals(ApplicationPaths.LegacyPreviousDeltas, StringComparison.OrdinalIgnoreCase),
                static legacyReplay => legacyReplay.Run(static context =>
                {
                    context.Response.ContentType = "application/json";
                    return context.Response.WriteAsync("[]", context.RequestAborted);
                }));

            next(app);
        };
    }
}
