// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;
using System.IO;

namespace Microsoft.DotNet.HotReload;

internal sealed class ForwardingBrowserToolsLaunchConfigurator(
    string middlewareAssemblyPath,
    AbstractBrowserRefreshServer browserRefreshServer) : IBrowserToolsLaunchConfigurator
{
    public void ConfigureLaunchEnvironment(IDictionary<string, string> environment)
    {
        environment[MiddlewareEnvironmentVariables.AspNetCoreAutoReloadProviderAddress] = browserRefreshServer.ProviderAddress.AbsoluteUri;

        // Loading the assembly as a startup hook makes the out-of-application BrowserRefresh
        // assembly resolvable when ASP.NET Core activates its hosting startup by simple name.
        environment.InsertListItem(MiddlewareEnvironmentVariables.DotNetStartupHooks, middlewareAssemblyPath, Path.PathSeparator);
        environment.InsertListItem(
            MiddlewareEnvironmentVariables.AspNetCoreHostingStartupAssemblies,
            Path.GetFileNameWithoutExtension(middlewareAssemblyPath),
            MiddlewareEnvironmentVariables.AspNetCoreHostingStartupAssembliesSeparator);

        if (browserRefreshServer.Logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Trace))
        {
            environment[MiddlewareEnvironmentVariables.LoggingLevel] = "Debug";
        }
    }
}
