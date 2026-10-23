// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli;

/// <summary>
/// OpenHarmony CLI defaults for processes spawned by the SDK. The sandbox settings
/// (JIT W^X, invariant globalization) are written into the SDK tools' runtimeconfigs;
/// these environment defaults only opt out of telemetry and the first-run logo, which
/// are harmless for user applications. Only active on OpenHarmony.
/// </summary>
internal static class OpenHarmonyEnvironmentDefaults
{
    /// <summary>
    /// The defaults applied to processes the CLI spawns (pinned by tests). TMPDIR is
    /// deliberately absent: Path.GetTempPath() honors the host-provided value, matching the
    /// runtime contract; the install script persists a writable one for the on-device shells.
    /// </summary>
    internal static (string Name, string Value)[] Defaults { get; } =
    {
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
