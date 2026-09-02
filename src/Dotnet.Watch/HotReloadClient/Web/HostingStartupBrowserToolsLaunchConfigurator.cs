// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;
using System.IO;

namespace Microsoft.DotNet.HotReload;

internal sealed class HostingStartupBrowserToolsLaunchConfigurator(
    string middlewareAssemblyPath,
    AbstractBrowserRefreshServer browserRefreshServer,
    bool enableManagedHotReload) : IBrowserToolsLaunchConfigurator
{
    public void ConfigureLaunchEnvironment(IDictionary<string, string> environment)
    {
        environment[MiddlewareEnvironmentVariables.AspNetCoreAutoReloadWSEndPoint] = string.Join(",", browserRefreshServer.WebSocketEndpoints);
        environment[MiddlewareEnvironmentVariables.AspNetCoreAutoReloadWSKey] = browserRefreshServer.PublicKey;
        environment[MiddlewareEnvironmentVariables.AspNetCoreAutoReloadVirtualDirectory] = browserRefreshServer.VirtualDirectory;
        environment.InsertListItem(MiddlewareEnvironmentVariables.DotNetStartupHooks, middlewareAssemblyPath, Path.PathSeparator);
        environment.InsertListItem(
            MiddlewareEnvironmentVariables.AspNetCoreHostingStartupAssemblies,
            Path.GetFileNameWithoutExtension(middlewareAssemblyPath),
            MiddlewareEnvironmentVariables.AspNetCoreHostingStartupAssembliesSeparator);

        if (enableManagedHotReload)
        {
            environment[MiddlewareEnvironmentVariables.DotNetModifiableAssemblies] = "debug";
        }

        if (browserRefreshServer.Logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Trace))
        {
            environment[MiddlewareEnvironmentVariables.LoggingLevel] = "Debug";
        }
    }
}
