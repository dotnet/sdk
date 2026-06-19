// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Bootstrapper.Shell;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Builds the paste-able shell command that activates the configured env state in the
/// <em>current</em> terminal (e.g. <c>eval "$(dotnetup env script)"</c>), by delegating to the
/// detected shell provider's <see cref="IEnvShellProvider.GenerateActivationCommand"/>. The
/// generated <c>env script</c> call carries no flags — it follows the stored config and auto-detects
/// the shell at run time, which (because the command is offered right after the config is written /
/// from the current config) reproduces the configured state. Returns <c>null</c> when there is
/// nothing to activate (no shell detected, or the config wires neither dotnet nor dotnetup) — the
/// activation command can only add managed directories to the session, so a pure removal has no
/// command to offer.
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

        bool wiresDotnet = accessMode is DotnetAccessMode.Shell or DotnetAccessMode.Full;
        if (!wiresDotnet && !dotnetupOnPath)
        {
            return null;
        }

        string dotnetupPath = ShellProviderHelpers.GetDotnetupExecutablePathOrThrow();
        return shellProvider.GenerateActivationCommand(dotnetupPath);
    }
}
