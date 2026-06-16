// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Bootstrapper.Shell;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;

/// <summary>
/// Shared logic for the <c>env set</c> and <c>env clear</c> commands: resolves the target
/// settings against the stored config, applies them via <see cref="PathPreferenceApplier"/>,
/// persists the new config, and prints a short summary. Keeping this in one place ensures
/// <c>env clear</c> and <c>env set none --dotnetup-on-path off</c> behave identically.
/// </summary>
internal static class EnvSettingsWriter
{
    public static void ApplyAndPersist(
        PathPreference targetEnv,
        bool targetDotnetupOnPath,
        IDotnetEnvironmentManager environment,
        IEnvShellProvider? shellProvider)
    {
        DotnetupConfigData? previous = DotnetupConfig.Read();

        string dotnetRoot = environment.GetDefaultDotnetInstallPath();

        PathPreferenceApplier.Apply(
            targetEnv,
            targetDotnetupOnPath,
            previous?.Env,
            previous?.DotnetupOnPath,
            environment,
            dotnetRoot,
            shellProvider);

        DotnetupConfig.Write(new DotnetupConfigData
        {
            Env = targetEnv,
            DotnetupOnPath = targetDotnetupOnPath,
        });

        Console.WriteLine($"dotnetup env: dotnet exposure '{targetEnv.ToString().ToLowerInvariant()}', dotnetup on PATH {(targetDotnetupOnPath ? "on" : "off")}.");
        Console.WriteLine("NOTE: You may need to restart your terminal for the changes to take effect.");
    }
}
