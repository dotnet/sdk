// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.DotNet.HotReload;

internal static class MiddlewareEnvironmentVariables
{
    /// <summary>
    /// dotnet runtime environment variable used to load middleware assembly into the web server process.
    /// https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-environment-variables#dotnet_startup_hooks
    /// </summary>
    public const string DotNetStartupHooks = "DOTNET_STARTUP_HOOKS";

    /// <summary>
    /// Simple names of assemblies that implement middleware components to be added to the web server.
    /// </summary>
    public const string AspNetCoreHostingStartupAssemblies = "ASPNETCORE_HOSTINGSTARTUPASSEMBLIES";
    public const char AspNetCoreHostingStartupAssembliesSeparator = ';';

    public const string AspNetCoreAutoReloadProviderAddress = "ASPNETCORE_AUTO_RELOAD_PROVIDER_ADDRESS";

    /// <summary>
    /// Variable used to set the logging level of the middleware logger.
    /// </summary>
    public const string LoggingLevel = "Logging__LogLevel__Microsoft.AspNetCore.Watch";
}
