// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch;

internal static partial class EnvironmentVariables
{
    public static partial class Names
    {
        /// <summary>
        /// Intentionally different from the variable name used by the debugger.
        /// This is to avoid the debugger colliding with dotnet-watch pipe connection when debugging dotnet-watch (or tests).
        /// </summary>
        public const string DotnetWatchHotReloadNamedPipeName = "DOTNET_WATCH_HOTRELOAD_NAMEDPIPE_NAME";

        /// <summary>
        /// Enables logging from the client delta applier agent.
        /// </summary>
        public const string HotReloadDeltaClientLogMessages = "HOTRELOAD_DELTA_CLIENT_LOG_MESSAGES";

        public const string DotnetStartupHooks = "DOTNET_STARTUP_HOOKS";
        public const string DotnetModifiableAssemblies = "DOTNET_MODIFIABLE_ASSEMBLIES";
    }
}
