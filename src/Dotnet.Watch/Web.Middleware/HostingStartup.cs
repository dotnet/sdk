// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
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
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITagHelperComponent, BrowserRefreshTagHelperComponent>());
    }

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.MapWhen(
                static context => context.Request.Path.StartsWithSegments(ApplicationPaths.BrowserTools),
                static browserTools =>
                {
                    browserTools.UseWebSockets();
                    browserTools.Run(
                        context => context.RequestServices.GetRequiredService<BrowserToolsForwarder>().ForwardAsync(context));
                });
            next(app);
        };
    }
}
