// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Bootstrapper.Shell;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Builds the paste-able shell command that activates the configured env state in the
/// <em>current</em> terminal (e.g. <c>eval "$(dotnetup env script --shell bash --dotnet --dotnetup)"</c>),
/// by delegating to the detected shell provider's <see cref="IEnvShellProvider.GenerateActivationCommand"/>.
/// Returns <c>null</c> when there is nothing to activate (no shell detected, or the target wires
/// neither dotnet nor dotnetup) — the activation command can only add managed directories to the
/// session, so a pure removal has no command to offer.
/// </summary>
internal static class EnvActivationCommandBuilder
{
    public static string? TryBuild(
        IEnvShellProvider? shellProvider,
        DotnetAccessMode accessMode,
        bool dotnetupOnPath)
    {
        if (shellProvider is null)
        {
            return null;
        }

        bool includeDotnet = accessMode is DotnetAccessMode.Shell or DotnetAccessMode.All;
        bool includeDotnetup = dotnetupOnPath;
        if (!includeDotnet && !includeDotnetup)
        {
            return null;
        }

        string dotnetupPath = ShellProviderHelpers.GetDotnetupExecutablePathOrThrow();
        return shellProvider.GenerateActivationCommand(dotnetupPath, includeDotnet, includeDotnetup);
    }
}
