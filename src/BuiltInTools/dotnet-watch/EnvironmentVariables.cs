// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watcher;

internal static class EnvironmentVariables
{
    public static bool VerboseCliOutput => ReadBool("DOTNET_CLI_CONTEXT_VERBOSE");
    public static bool IsPollingEnabled => ReadBool("DOTNET_USE_POLLING_FILE_WATCHER");
    public static bool SuppressEmojis => ReadBool("DOTNET_WATCH_SUPPRESS_EMOJIS");
    public static bool RestartOnRudeEdit => ReadBool("DOTNET_WATCH_RESTART_ON_RUDE_EDIT");

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

    public static string? AutoReloadWSHostName => Environment.GetEnvironmentVariable("DOTNET_WATCH_AUTO_RELOAD_WS_HOSTNAME");
    public static string? BrowserPath => Environment.GetEnvironmentVariable("DOTNET_WATCH_BROWSER_PATH");

    private static bool ReadBool(string variableName)
        => Environment.GetEnvironmentVariable(variableName) is var value && (value == "1" || bool.TryParse(value, out var boolValue) && boolValue);
}
