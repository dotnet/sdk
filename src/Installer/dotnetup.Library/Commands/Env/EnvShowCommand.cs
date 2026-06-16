// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Dotnet.Installation.Internal;
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
        DotnetupConfigData? config = DotnetupConfig.Read();
        if (config is null)
        {
            Console.WriteLine("No dotnetup env configuration found. Run 'dotnetup env set <mode>' to configure.");
            return;
        }

        Console.WriteLine("dotnetup environment:");
        Console.WriteLine($"  dotnet exposure    {config.Env.ToString().ToLowerInvariant()}");
        Console.WriteLine($"  dotnetup on PATH   {(config.DotnetupOnPath ? "yes" : "no")}");

        var drift = DetectDrift(config);
        if (drift.Count == 0)
        {
            Console.WriteLine();
            Console.WriteLine("In sync.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Detected drift between configured settings and the current environment:");
        foreach (var item in drift)
        {
            Console.WriteLine($"  - {item}");
        }
        Console.WriteLine();
        Console.WriteLine("Run 'dotnetup env set' to re-sync.");
    }

    private List<string> DetectDrift(DotnetupConfigData config)
    {
        var drift = new List<string>();
        var shellProvider = _shellProvider ?? ShellDetection.GetCurrentShellProvider();

        bool expectsProfileDotnet = config.Env is PathPreference.Shell or PathPreference.All;
        bool expectsProfileBlock = expectsProfileDotnet || config.DotnetupOnPath;
        bool expectsDotnetEnvVars = config.Env == PathPreference.All;

        // Profile-block presence check (best-effort; we only assert presence/absence, not contents).
        if (shellProvider is not null)
        {
            var profilePaths = shellProvider.GetProfilePaths();
            var profilesWithEntries = ShellProfileManager.GetProfilePathsWithEntries(shellProvider);

            if (expectsProfileBlock && profilesWithEntries.Count == 0 && profilePaths.Count > 0)
            {
                drift.Add("Shell profile is missing the dotnetup managed block.");
            }
            else if (!expectsProfileBlock && profilesWithEntries.Count > 0)
            {
                drift.Add("Shell profile contains a dotnetup managed block but neither dotnet exposure nor dotnetup-on-PATH is configured.");
            }
        }

        if (OperatingSystem.IsWindows())
        {
            var installRootManager = new InstallRootManager(_dotnetEnvironment);
            if (expectsDotnetEnvVars)
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
                    drift.Add($"Windows user PATH / DOTNET_ROOT still has 'all'-mode wiring (expected dotnet exposure: '{config.Env.ToString().ToLowerInvariant()}').");
                }
            }

            // The user-scope PATH is authoritative for dotnetup-on-PATH on Windows (the profile
            // block copy is just a convenience).
            bool dotnetupOnUserPath = UserPathContainsDotnetupDir();
            if (config.DotnetupOnPath && !dotnetupOnUserPath)
            {
                drift.Add("dotnetup is configured to be on PATH but is missing from the user PATH.");
            }
            else if (!config.DotnetupOnPath && dotnetupOnUserPath)
            {
                drift.Add("dotnetup is on the user PATH but is configured to be off.");
            }
        }

        return drift;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool UserPathContainsDotnetupDir()
    {
        string dotnetupDir = ShellProviderHelpers.GetDotnetupDirectoryOrThrow();
        return WindowsPathHelper.SplitPath(WindowsPathHelper.ReadUserPath(expand: true))
            .Any(entry => DotnetupUtilities.PathsEqual(entry, dotnetupDir));
    }
}
