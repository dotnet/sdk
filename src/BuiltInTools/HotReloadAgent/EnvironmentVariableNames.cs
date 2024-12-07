// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.HotReload;

internal static class EnvironmentVariableNames
{
    /// <summary>
    /// Intentionally different from the variable name used by the debugger.
    /// This is to avoid the debugger colliding with dotnet-watch pipe connection when debugging dotnet-watch (or tests).
    /// </summary>
    public const string DotNetWatchHotReloadNamedPipeName = "DOTNET_WATCH_HOTRELOAD_NAMEDPIPE_NAME";

    /// <summary>
    /// The full path to the process being launched by dotnet run.
    /// Workaround for https://github.com/dotnet/sdk/issues/40484
    /// </summary>
    public const string DotNetWatchHotReloadTargetProcessPath = "DOTNET_WATCH_HOTRELOAD_TARGET_PROCESS_PATH";

    /// <summary>
    /// Enables logging from the client delta applier agent.
    /// </summary>
    public const string HotReloadDeltaClientLogMessages = "HOTRELOAD_DELTA_CLIENT_LOG_MESSAGES";

    /// <summary>
    /// dotnet runtime environment variable.
    /// https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-environment-variables#dotnet_startup_hooks
    /// </summary>
    public const string DotNetStartupHooks = "DOTNET_STARTUP_HOOKS";

    /// <summary>
    /// dotnet runtime environment variable.
    /// </summary>
    public const string DotNetModifiableAssemblies = "DOTNET_MODIFIABLE_ASSEMBLIES";
}
