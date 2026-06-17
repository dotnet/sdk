// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;

/// <summary>
/// Decides which aspects <c>env script</c> should wire from the selection flags and the stored
/// config. Explicit flags always win. With no flags, the stored config drives the result — so a
/// bare <c>eval "$(dotnetup env script)"</c> activates the user's configured setup — falling back to
/// wiring both when no config exists yet (which preserves the behavior the legacy
/// <c>print-env-script</c> profile blocks rely on). Kept free of console / environment access so it
/// is unit-testable. Profile blocks and activation commands bake explicit flags, so they never
/// depend on the no-flag default.
/// </summary>
internal static class EnvScriptSelectionResolver
{
    public static EnvScriptSelection Resolve(bool dotnet, bool dotnetup, bool dotnetupOnly, DotnetupConfigData? config)
    {
        if (dotnetupOnly)
        {
            if (dotnet || dotnetup)
            {
                throw new DotnetInstallException(
                    DotnetInstallErrorCode.Unknown,
                    "--dotnetup-only cannot be combined with --dotnet or --dotnetup.");
            }

            return new EnvScriptSelection(IncludeDotnet: false, IncludeDotnetup: true);
        }

        if (dotnet || dotnetup)
        {
            return new EnvScriptSelection(IncludeDotnet: dotnet, IncludeDotnetup: dotnetup);
        }

        // No selection flags: use the stored config if present, otherwise wire both.
        if (config is null)
        {
            return new EnvScriptSelection(IncludeDotnet: true, IncludeDotnetup: true);
        }

        bool includeDotnet = config.AccessMode is DotnetAccessMode.Shell or DotnetAccessMode.All;
        return new EnvScriptSelection(IncludeDotnet: includeDotnet, IncludeDotnetup: config.DotnetupOnPath);
    }
}

/// <summary>
/// Which aspects an <c>env script</c> invocation should wire: the managed dotnet
/// (<c>DOTNET_ROOT</c> + dotnet on PATH) and/or the dotnetup directory on PATH.
/// </summary>
internal sealed record EnvScriptSelection(bool IncludeDotnet, bool IncludeDotnetup);
