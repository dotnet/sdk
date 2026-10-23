// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;

/// <summary>
/// Decides which aspects <c>env script</c> should wire from the selection flags and the stored
/// config. Explicit flags win, except that admin mode never activates managed dotnet. With no flags,
/// the stored config drives the result — so a
/// bare <c>eval "$(dotnetup env script)"</c> activates the user's configured setup — falling back to
/// wiring both when no config exists yet (which preserves the behavior the legacy
/// <c>print-env-script</c> profile blocks rely on). Kept free of console / environment access so it
/// is unit-testable. Profile blocks and activation commands bake explicit flags, so they never
/// depend on the no-flag default.
/// </summary>
internal static class EnvScriptSelectionResolver
{
    /// <param name="dotnetRequested">
    /// True when <c>--dotnet</c> was passed. False means "not explicitly requested" (not
    /// "exclude dotnet"): when neither this nor <paramref name="dotnetupRequested"/> is set, the
    /// stored config decides.
    /// </param>
    /// <param name="dotnetupRequested">
    /// True when <c>--dotnetup</c> was passed. False means "not explicitly requested" (not
    /// "exclude dotnetup"); see <paramref name="dotnetRequested"/>.
    /// </param>
    /// <param name="dotnetupOnly">
    /// True when <c>--dotnetup-only</c> was passed: wire only dotnetup, never dotnet. Cannot be
    /// combined with <paramref name="dotnetRequested"/> or <paramref name="dotnetupRequested"/>.
    /// </param>
    /// <param name="config">The stored config, or <c>null</c> when none exists yet.</param>
    /// <param name="isAdminMode">Suppress managed dotnet activation, including explicit requests from existing profiles.</param>
    public static EnvScriptSelection Resolve(bool dotnetRequested, bool dotnetupRequested, bool dotnetupOnly, DotnetupConfigData? config, bool isAdminMode = false)
    {
        if (dotnetupOnly)
        {
            if (dotnetRequested || dotnetupRequested)
            {
                throw new DotnetInstallException(
                    DotnetInstallErrorCode.InvalidArguments,
                    Strings.EnvScriptDotnetupOnlyConflict);
            }

            return new EnvScriptSelection(IncludeDotnet: false, IncludeDotnetup: true);
        }

        if (dotnetRequested || dotnetupRequested)
        {
            return new EnvScriptSelection(IncludeDotnet: dotnetRequested && !isAdminMode, IncludeDotnetup: dotnetupRequested);
        }

        // No selection flags: use the stored config if present, otherwise wire both.
        if (config is null)
        {
            return new EnvScriptSelection(IncludeDotnet: !isAdminMode, IncludeDotnetup: true);
        }

        bool includeDotnet = !isAdminMode && config.AccessMode is DotnetAccessMode.Shell or DotnetAccessMode.Everywhere;
        return new EnvScriptSelection(IncludeDotnet: includeDotnet, IncludeDotnetup: config.DotnetupOnPath);
    }
}

/// <summary>
/// Which aspects an <c>env script</c> invocation should wire: the managed dotnet
/// (<c>DOTNET_ROOT</c> + dotnet on PATH) and/or the dotnetup directory on PATH.
/// </summary>
internal sealed record EnvScriptSelection(bool IncludeDotnet, bool IncludeDotnetup);
