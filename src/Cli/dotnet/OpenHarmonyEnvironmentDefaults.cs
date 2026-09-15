// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli;

/// <summary>
/// OpenHarmony sandbox defaults for processes spawned by the CLI. The sandbox blocks JIT W^X
/// mprotect and ships no ICU; baked runtimeconfig options cover the SDK's own processes,
/// and these environment defaults cover every child process (MSBuild, csc, apphosts) that
/// inherits the CLI's environment. Only active on OpenHarmony.
/// </summary>
internal static class OpenHarmonyEnvironmentDefaults
{
    /// <summary>
    /// The defaults applied to every process the CLI spawns (pinned by tests). TMPDIR is
    /// deliberately absent: Path.GetTempPath() honors the host-provided value, matching the
    /// runtime contract; the install script persists a writable one for the on-device shells.
    /// </summary>
    internal static (string Name, string Value)[] Defaults { get; } =
    {
        ("DOTNET_EnableWriteXorExecute", "0"),
        ("DOTNET_SYSTEM_GLOBALIZATION_INVARIANT", "1"),
        (EnvironmentVariableNames.TELEMETRY_OPTOUT, "1"),
        (EnvironmentVariableNames.DOTNET_NOLOGO, "1"),
    };

    public static void Apply()
    {
        if (!OperatingSystem.IsOSPlatform("openharmony"))
        {
            return;
        }

        foreach ((string name, string value) in Defaults)
        {
            SetDefault(name, value);
        }
    }

    private static void SetDefault(string name, string value)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)))
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }
}
