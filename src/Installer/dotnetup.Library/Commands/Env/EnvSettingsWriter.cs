// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Bootstrapper.Shell;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;

/// <summary>
/// Shared apply path for the <c>env set</c> and <c>env clear</c> commands: inspects the live
/// environment, applies the target settings via <see cref="EnvSettingsApplier"/>, persists the
/// new config, and prints a short summary. Keeping this in one place ensures <c>env clear</c> and
/// <c>env set none --dotnetup-on-path off</c> behave identically.
/// </summary>
internal static class EnvSettingsWriter
{
    public static void ApplyAndPersist(
        DotnetAccessMode targetEnv,
        bool targetDotnetupOnPath,
        IDotnetEnvironmentManager environment,
        IEnvShellProvider? shellProvider,
        IEnvironmentStateInspector inspector)
    {
        IEnvShellProvider? resolvedShellProvider = shellProvider ?? ShellDetection.GetCurrentShellProvider();

        string dotnetRoot = environment.GetDefaultDotnetInstallPath();

        // Removal decisions come from the live environment, not the stored config, so drift is
        // corrected on re-sync.
        ObservedEnvironmentState observed = inspector.Inspect(resolvedShellProvider);

        EnvSettingsApplier.Apply(
            targetEnv,
            targetDotnetupOnPath,
            observed,
            environment,
            dotnetRoot,
            resolvedShellProvider);

        DotnetupConfig.Write(new DotnetupConfigData
        {
            AccessMode = targetEnv,
            DotnetupOnPath = targetDotnetupOnPath,
        });

        Console.WriteLine($"dotnetup env: dotnet access '{targetEnv.ToString().ToLowerInvariant()}', dotnetup on PATH {(targetDotnetupOnPath ? "on" : "off")}.");
        Console.WriteLine("NOTE: You may need to restart your terminal for the changes to take effect.");
    }
}
