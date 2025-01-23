// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch;

internal static class EnvironmentVariables
{
    public static class Names
    {
        public const string DotnetWatch = "DOTNET_WATCH";
        public const string DotnetWatchIteration = "DOTNET_WATCH_ITERATION";

        public const string DotnetLaunchProfile = "DOTNET_LAUNCH_PROFILE";

        public const string AspNetCoreUrls = "ASPNETCORE_URLS";
        public const string AspNetCoreHostingStartupAssemblies = "ASPNETCORE_HOSTINGSTARTUPASSEMBLIES";
        public const string AspNetCoreAutoReloadWSEndPoint = "ASPNETCORE_AUTO_RELOAD_WS_ENDPOINT";
        public const string AspNetCoreAutoReloadWSKey = "ASPNETCORE_AUTO_RELOAD_WS_KEY";

        public const string DotNetWatchHotReloadNamedPipeName = HotReload.AgentEnvironmentVariables.DotNetWatchHotReloadNamedPipeName;
        public const string DotNetWatchHotReloadTargetProcessPath = HotReload.AgentEnvironmentVariables.DotNetWatchHotReloadTargetProcessPath;
        public const string DotNetStartupHooks = HotReload.AgentEnvironmentVariables.DotNetStartupHooks;
        public const string DotNetModifiableAssemblies = HotReload.AgentEnvironmentVariables.DotNetModifiableAssemblies;
        public const string HotReloadDeltaClientLogMessages = HotReload.AgentEnvironmentVariables.HotReloadDeltaClientLogMessages;
    }

    public static bool VerboseCliOutput => ReadBool("DOTNET_CLI_CONTEXT_VERBOSE");
    public static bool IsPollingEnabled => ReadBool("DOTNET_USE_POLLING_FILE_WATCHER");
    public static bool SuppressEmojis => ReadBool("DOTNET_WATCH_SUPPRESS_EMOJIS");
    public static bool RestartOnRudeEdit => ReadBool("DOTNET_WATCH_RESTART_ON_RUDE_EDIT");
    public static TimeSpan ProcessCleanupTimeout => ReadTimeSpan("DOTNET_WATCH_PROCESS_CLEANUP_TIMEOUT_MS");

    public static string SdkRootDirectory =>
#if DEBUG
        Environment.GetEnvironmentVariable("DOTNET_WATCH_DEBUG_SDK_DIRECTORY") ?? "";
#else
        "";
#endif

    public static bool SuppressHandlingStaticContentFiles => ReadBool("DOTNET_WATCH_SUPPRESS_STATIC_FILE_HANDLING");
    public static bool SuppressMSBuildIncrementalism => ReadBool("DOTNET_WATCH_SUPPRESS_MSBUILD_INCREMENTALISM");
    public static bool SuppressLaunchBrowser => ReadBool("DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER");
    public static bool SuppressBrowserRefresh => ReadBool("DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH");

    public static TestFlags TestFlags => Environment.GetEnvironmentVariable("__DOTNET_WATCH_TEST_FLAGS") is { } value ? Enum.Parse<TestFlags>(value) : TestFlags.None;
    public static string TestOutputDir => Environment.GetEnvironmentVariable("__DOTNET_WATCH_TEST_OUTPUT_DIR") ?? "";  

    public static string? AutoReloadWSHostName => Environment.GetEnvironmentVariable("DOTNET_WATCH_AUTO_RELOAD_WS_HOSTNAME");
    public static string? BrowserPath => Environment.GetEnvironmentVariable("DOTNET_WATCH_BROWSER_PATH");

    private static bool ReadBool(string variableName)
        => Environment.GetEnvironmentVariable(variableName) is var value && (value == "1" || bool.TryParse(value, out var boolValue) && boolValue);

    private static TimeSpan ReadTimeSpan(string variableName)
        => Environment.GetEnvironmentVariable(variableName) is var value && long.TryParse(value, out var intValue) && intValue >= 0 ? TimeSpan.FromMilliseconds(intValue) : TimeSpan.FromSeconds(5);
}
