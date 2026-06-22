// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// A snapshot of what dotnetup has actually wired into the current environment, read from the
/// live machine state (shell profile, Windows user-scope env vars, user PATH) rather than from
/// the stored config. This is the env-axis analog of <see cref="UserInstallRootChanges"/>:
/// <see cref="EnvironmentStateInspector"/> produces it, <see cref="EnvSettingsApplier"/>
/// consumes it to decide what to remove, and <see cref="Commands.Env.EnvDriftAnalyzer"/> compares
/// it against the configured settings to report drift.
/// </summary>
/// <param name="DotnetUserEnvVarsPresent">
/// True when user-scope dotnet env-var wiring (user PATH entry and/or DOTNET_ROOT) that
/// <c>all</c> mode creates is currently present. Always false off Windows.
/// </param>
/// <param name="DotnetUserEnvVarsComplete">
/// True when the user-scope dotnet env vars are fully in the <c>all</c>-mode state (nothing left
/// to wire). Always false off Windows. Used to detect a configured-<c>all</c>-but-incomplete drift.
/// </param>
/// <param name="ProfileBlockPresent">
/// Whether the managed dotnetup block exists in the shell profile, or <c>null</c> when the shell
/// could not be determined (profile state unknown — neither presence nor absence asserted).
/// </param>
/// <param name="DotnetupOnUserPath">
/// True when the dotnetup directory is on the Windows user-scope PATH. Always false off Windows,
/// where dotnetup-on-PATH is realized entirely through the shell profile block.
/// </param>
internal sealed record ObservedEnvironmentState(
    bool DotnetUserEnvVarsPresent,
    bool DotnetUserEnvVarsComplete,
    bool? ProfileBlockPresent,
    bool DotnetupOnUserPath)
{
    /// <summary>
    /// Nothing observed as wired, with profile state unknown. Suitable as the "previous state"
    /// for a first-time application where no prior wiring is assumed.
    /// </summary>
    public static ObservedEnvironmentState Empty { get; } = new(
        DotnetUserEnvVarsPresent: false,
        DotnetUserEnvVarsComplete: false,
        ProfileBlockPresent: null,
        DotnetupOnUserPath: false);
}
