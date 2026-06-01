// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;

internal class EnvShowCommand : CommandBase
{
    private readonly IDotnetEnvironmentManager _dotnetEnvironment;
    private readonly IEnvShellProvider? _shellProvider;

    public EnvShowCommand(ParseResult result, IDotnetEnvironmentManager? dotnetEnvironment = null) : base(result)
    {
        _dotnetEnvironment = dotnetEnvironment ?? new DotnetEnvironmentManager();
        _shellProvider = result.GetValue(CommonOptions.ShellOption);
    }

    protected override string GetCommandName() => "env show";

    protected override void ExecuteCore()
    {
        PathPreference? configured = DotnetupConfig.ReadPathPreference();
        if (configured is null)
        {
            Console.WriteLine("No dotnetup env configuration found. Run 'dotnetup env set <mode>' to configure.");
            return;
        }

        Console.WriteLine($"Configured env mode: {configured.Value.ToString().ToLowerInvariant()}");

        var drift = DetectDrift(configured.Value);
        if (drift.Count == 0)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Detected drift between configured mode and current environment:");
        foreach (var item in drift)
        {
            Console.WriteLine($"  - {item}");
        }
        Console.WriteLine();
        Console.WriteLine($"Run 'dotnetup env set {configured.Value.ToString().ToLowerInvariant()}' to re-sync.");
    }

    private List<string> DetectDrift(PathPreference configured)
    {
        var drift = new List<string>();
        var shellProvider = _shellProvider ?? ShellDetection.GetCurrentShellProvider();

        bool expectsProfile = configured is PathPreference.Shell or PathPreference.All;
        bool expectsEnvVars = configured == PathPreference.All;

        if (shellProvider is not null)
        {
            var profilePaths = shellProvider.GetProfilePaths();
            var profilesWithEntries = ShellProfileManager.GetProfilePathsWithEntries(shellProvider);

            if (expectsProfile && profilesWithEntries.Count == 0 && profilePaths.Count > 0)
            {
                drift.Add("Shell profile is missing the dotnetup managed block.");
            }
            else if (!expectsProfile && profilesWithEntries.Count > 0)
            {
                drift.Add($"Shell profile contains a dotnetup managed block but mode is '{configured.ToString().ToLowerInvariant()}'.");
            }
        }

        if (OperatingSystem.IsWindows())
        {
            var installRootManager = new InstallRootManager(_dotnetEnvironment);
            if (expectsEnvVars)
            {
                var changes = installRootManager.GetUserInstallRootChanges();
                if (changes.NeedsChange())
                {
                    drift.Add("Windows user PATH / DOTNET_ROOT / system PATH do not match 'all' mode expectations.");
                }
            }
            else
            {
                var adminChanges = installRootManager.GetAdminInstallRootChanges();
                if (adminChanges.NeedsChange())
                {
                    drift.Add($"Windows user PATH / DOTNET_ROOT still has 'all'-mode wiring (expected: '{configured.ToString().ToLowerInvariant()}').");
                }
            }
        }

        return drift;
    }
}
