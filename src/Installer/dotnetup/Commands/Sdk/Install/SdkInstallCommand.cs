// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Net.Http;
using System.Runtime.InteropServices;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Install;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Spectre.Console;
using SpectreAnsiConsole = Spectre.Console.AnsiConsole;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Install;

internal class SdkInstallCommand(ParseResult result) : CommandBase(result)
{
    private const string ComponentDescription = ".NET SDK";

    private readonly string? _versionOrChannel = result.GetValue(SdkInstallCommandParser.ChannelArgument);
    private readonly string? _installPath = result.GetValue(SdkInstallCommandParser.InstallPathOption);
    private readonly bool? _setDefaultInstall = result.GetValue(SdkInstallCommandParser.SetDefaultInstallOption);
    private readonly bool? _updateGlobalJson = result.GetValue(SdkInstallCommandParser.UpdateGlobalJsonOption);
    private readonly string? _manifestPath = result.GetValue(SdkInstallCommandParser.ManifestPathOption);
    private readonly bool _interactive = result.GetValue(SdkInstallCommandParser.InteractiveOption);
    private readonly bool _noProgress = result.GetValue(SdkInstallCommandParser.NoProgressOption);

    private readonly IDotnetInstallManager _dotnetInstaller = new DotnetInstallManager();
    private readonly ChannelVersionResolver _channelVersionResolver = new ChannelVersionResolver();
    private InstallPathResolver? _installPathResolver;
    private InstallWalkthrough? _installWalkthrough;

    private InstallPathResolver InstallPathResolver => _installPathResolver ??= new InstallPathResolver(_dotnetInstaller);
    private InstallWalkthrough InstallWalkthrough => _installWalkthrough ??= new InstallWalkthrough(_dotnetInstaller, _channelVersionResolver);

    public override int Execute()
    {
        var globalJsonInfo = _dotnetInstaller.GetGlobalJsonInfo(Environment.CurrentDirectory);
        var currentDotnetInstallRoot = _dotnetInstaller.GetConfiguredInstallType();

        // Resolve the install path using the shared resolver
        var pathResolution = InstallPathResolver.Resolve(
            _installPath,
            globalJsonInfo,
            currentDotnetInstallRoot,
            _interactive,
            ComponentDescription,
            out var error);

        if (pathResolution == null)
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        string resolvedInstallPath = pathResolution.ResolvedInstallPath;
        string? installPathFromGlobalJson = pathResolution.InstallPathFromGlobalJson;

        string? channelFromGlobalJson = null;
        if (globalJsonInfo?.GlobalJsonPath is not null)
        {
            channelFromGlobalJson = ResolveChannelFromGlobalJson(globalJsonInfo.GlobalJsonPath);
        }

        // Check if global.json should be updated
        bool? resolvedUpdateGlobalJson = InstallWalkthrough.ResolveUpdateGlobalJson(
            channelFromGlobalJson,
            _versionOrChannel,
            _updateGlobalJson,
            _interactive);

        // Resolve the channel/version to install
        string resolvedChannel = InstallWalkthrough.ResolveChannel(
            _versionOrChannel,
            channelFromGlobalJson,
            globalJsonInfo?.GlobalJsonPath,
            _interactive,
            ComponentDescription);

        // Resolve whether to set this as the default install
        bool resolvedSetDefaultInstall = InstallWalkthrough.ResolveSetDefaultInstall(
            _setDefaultInstall,
            currentDotnetInstallRoot,
            resolvedInstallPath,
            installPathFromGlobalJson,
            _interactive);

        List<string> additionalVersionsToInstall = new();

        // Create a request and resolve it using the channel version resolver
        var installRequest = new DotnetInstallRequest(
            new DotnetInstallRoot(resolvedInstallPath, InstallerUtilities.GetDefaultInstallArchitecture()),
            new UpdateChannel(resolvedChannel),
            InstallComponent.SDK,
            new InstallRequestOptions
            {
                ManifestPath = _manifestPath
            });

        var resolvedVersion = _channelVersionResolver.Resolve(installRequest);

        if (resolvedSetDefaultInstall == true && currentDotnetInstallRoot?.InstallType == InstallType.Admin)
        {
            if (_interactive)
            {
                var latestAdminVersion = _dotnetInstaller.GetLatestInstalledAdminVersion();
                if (latestAdminVersion is not null && resolvedVersion < new ReleaseVersion(latestAdminVersion))
                {
                    SpectreAnsiConsole.WriteLine($"Since the admin installs of the .NET SDK will no longer be accessible, we recommend installing the latest admin installed " +
                        $"version ({latestAdminVersion}) to the new user install location.  This will make sure this version of the .NET SDK continues to be used for projects that don't specify a .NET SDK version in global.json.");

                    if (SpectreAnsiConsole.Confirm($"Also install .NET SDK {latestAdminVersion}?",
                        defaultValue: true))
                    {
                        additionalVersionsToInstall.Add(latestAdminVersion);
                    }
                }
            }
            else
            {
                //  TODO: Add command-line option for installing admin versions locally
            }
        }

        //  TODO: Implement transaction / rollback?

        SpectreAnsiConsole.MarkupLineInterpolated($"Installing .NET SDK [blue]{resolvedVersion}[/] to [blue]{resolvedInstallPath}[/]...");

        DotnetInstall? mainInstall;

        // Pass the _noProgress flag to the InstallerOrchestratorSingleton
        // The orchestrator will handle installation with or without progress based on the flag
        mainInstall = InstallerOrchestratorSingleton.Instance.Install(installRequest, _noProgress);
        if (mainInstall == null)
        {
            SpectreAnsiConsole.MarkupLine($"[red]Failed to install .NET SDK {resolvedVersion}[/]");
            return 1;
        }
        SpectreAnsiConsole.MarkupLine($"[green]Installed .NET SDK {mainInstall.Version}, available via {mainInstall.InstallRoot}[/]");

        // Install any additional versions
        foreach (var additionalVersion in additionalVersionsToInstall)
        {
            // Create the request for the additional version
            var additionalRequest = new DotnetInstallRequest(
                new DotnetInstallRoot(resolvedInstallPath, InstallerUtilities.GetDefaultInstallArchitecture()),
                new UpdateChannel(additionalVersion),
                InstallComponent.SDK,
                new InstallRequestOptions
                {
                    ManifestPath = _manifestPath
                });

            // Install the additional version with the same progress settings as the main installation
            DotnetInstall? additionalInstall = InstallerOrchestratorSingleton.Instance.Install(additionalRequest);
            if (additionalInstall == null)
            {
                SpectreAnsiConsole.MarkupLine($"[red]Failed to install additional .NET SDK {additionalVersion}[/]");
            }
            else
            {
                SpectreAnsiConsole.MarkupLine($"[green]Installed additional .NET SDK {additionalInstall.Version}, available via {additionalInstall.InstallRoot}[/]");
            }
        }

        if (resolvedSetDefaultInstall == true)
        {
            // Use ConfigureInstallType on all platforms (Windows uses InstallRootManager internally)
            _dotnetInstaller.ConfigureInstallType(InstallType.User, resolvedInstallPath);
        }

        if (resolvedUpdateGlobalJson == true)
        {
            _dotnetInstaller.UpdateGlobalJson(globalJsonInfo!.GlobalJsonPath!, resolvedVersion!.ToString(), globalJsonInfo.AllowPrerelease, globalJsonInfo.RollForward);
        }


        SpectreAnsiConsole.WriteLine($"Complete!");


        return 0;
    }



    string? ResolveChannelFromGlobalJson(string globalJsonPath)
    {
        //return null;
        //return "9.0";
        return Environment.GetEnvironmentVariable("DOTNET_TESTHOOK_GLOBALJSON_SDK_CHANNEL");
    }

}
