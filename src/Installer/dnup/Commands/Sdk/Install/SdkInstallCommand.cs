// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Net.Http;
using Microsoft.Deployment.DotNet.Releases;
using Spectre.Console;


using SpectreAnsiConsole = Spectre.Console.AnsiConsole;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Install;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Install;

internal class SdkInstallCommand(ParseResult result) : CommandBase(result)
{
    private readonly string? _versionOrChannel = result.GetValue(SdkInstallCommandParser.ChannelArgument);
    private readonly string? _installPath = result.GetValue(SdkInstallCommandParser.InstallPathOption);
    private readonly bool? _setDefaultInstall = result.GetValue(SdkInstallCommandParser.SetDefaultInstallOption);
    private readonly bool? _updateGlobalJson = result.GetValue(SdkInstallCommandParser.UpdateGlobalJsonOption);
    private readonly bool _interactive = result.GetValue(SdkInstallCommandParser.InteractiveOption);

    private readonly IBootstrapperController _dotnetInstaller = new BootstrapperController();
    private readonly IReleaseInfoProvider _releaseInfoProvider = new EnvironmentVariableMockReleaseInfoProvider();
    private readonly ManifestChannelVersionResolver _channelVersionResolver = new ManifestChannelVersionResolver();

    public override int Execute()
    {
        var globalJsonInfo = _dotnetInstaller.GetGlobalJsonInfo(Environment.CurrentDirectory);

        string? currentInstallPath;
        InstallType defaultInstallState = _dotnetInstaller.GetConfiguredInstallType(out currentInstallPath);

        string? resolvedInstallPath = null;

        string? installPathFromGlobalJson = null;
        if (globalJsonInfo?.GlobalJsonPath != null)
        {
            installPathFromGlobalJson = globalJsonInfo.SdkPath;

            if (installPathFromGlobalJson != null && _installPath != null &&
                !DnupUtilities.PathsEqual(installPathFromGlobalJson, _installPath))
            {
                //  TODO: Add parameter to override error
                Console.Error.WriteLine($"Error: The install path specified in global.json ({installPathFromGlobalJson}) does not match the install path provided ({_installPath}).");
                return 1;
            }

            resolvedInstallPath = installPathFromGlobalJson;
        }

        if (resolvedInstallPath == null)
        {
            resolvedInstallPath = _installPath;
        }

        if (resolvedInstallPath == null && defaultInstallState == InstallType.User)
        {
            //  If a user installation is already set up, we don't need to prompt for the install path
            resolvedInstallPath = currentInstallPath;
        }

        if (resolvedInstallPath == null)
        {
            if (_interactive)
            {
                resolvedInstallPath = SpectreAnsiConsole.Prompt(
                    new TextPrompt<string>("Where should we install the .NET SDK to?)")
                        .DefaultValue(_dotnetInstaller.GetDefaultDotnetInstallPath()));
            }
            else
            {
                //  If no install path is specified, use the default install path
                resolvedInstallPath = _dotnetInstaller.GetDefaultDotnetInstallPath();
            }
        }

        string? channelFromGlobalJson = null;
        if (globalJsonInfo?.GlobalJsonPath != null)
        {
            channelFromGlobalJson = ResolveChannelFromGlobalJson(globalJsonInfo.GlobalJsonPath);
        }

        bool? resolvedUpdateGlobalJson = null;

        if (channelFromGlobalJson != null && _versionOrChannel != null &&
            //  TODO: Should channel comparison be case-sensitive?
            !channelFromGlobalJson.Equals(_versionOrChannel, StringComparison.OrdinalIgnoreCase))
        {
            if (_interactive && _updateGlobalJson == null)
            {
                resolvedUpdateGlobalJson = SpectreAnsiConsole.Confirm(
                    $"The channel specified in global.json ({channelFromGlobalJson}) does not match the channel specified ({_versionOrChannel}). Do you want to update global.json to match the specified channel?",
                    defaultValue: true);
            }
        }

        string? resolvedChannel = null;

        if (channelFromGlobalJson != null)
        {
            SpectreAnsiConsole.WriteLine($".NET SDK {channelFromGlobalJson} will be installed since {globalJsonInfo?.GlobalJsonPath} specifies that version.");

            resolvedChannel = channelFromGlobalJson;
        }
        else if (_versionOrChannel != null)
        {
            resolvedChannel = _versionOrChannel;
        }
        else
        {
            if (_interactive)
            {

                SpectreAnsiConsole.WriteLine("Available supported channels: " + string.Join(' ', _releaseInfoProvider.GetAvailableChannels()));
                SpectreAnsiConsole.WriteLine("You can also specify a specific version (for example 9.0.304).");

                resolvedChannel = SpectreAnsiConsole.Prompt(
                    new TextPrompt<string>("Which channel of the .NET SDK do you want to install?")
                        .DefaultValue("latest"));
            }
            else
            {
                resolvedChannel = "latest"; // Default to latest if no channel is specified
            }
        }

        bool? resolvedSetDefaultInstall = _setDefaultInstall;

        if (resolvedSetDefaultInstall == null)
        {
            //  If global.json specified an install path, we don't prompt for setting the default install path (since you probably don't want to do that for a repo-local path)
            if (_interactive && installPathFromGlobalJson == null)
            {
                if (defaultInstallState == InstallType.None)
                {
                    resolvedSetDefaultInstall = SpectreAnsiConsole.Confirm(
                        $"Do you want to set the install path ({resolvedInstallPath}) as the default dotnet install? This will update the PATH and DOTNET_ROOT environment variables.",
                        defaultValue: true);
                }
                else if (defaultInstallState == InstallType.User)
                {
                    if (DnupUtilities.PathsEqual(resolvedInstallPath, currentInstallPath))
                    {
                        //  No need to prompt here, the default install is already set up.
                    }
                    else
                    {
                        resolvedSetDefaultInstall = SpectreAnsiConsole.Confirm(
                            $"The default dotnet install is currently set to {currentInstallPath}.  Do you want to change it to {resolvedInstallPath}?",
                            defaultValue: false);
                    }
                }
                else if (defaultInstallState == InstallType.Admin)
                {
                    SpectreAnsiConsole.WriteLine($"You have an existing admin install of .NET in {currentInstallPath}. We can configure your system to use the new install of .NET " +
                        $"in {resolvedInstallPath} instead. This would mean that the admin install of .NET would no longer be accessible from the PATH or from Visual Studio.");
                    SpectreAnsiConsole.WriteLine("You can change this later with the \"dotnet defaultinstall\" command.");
                    resolvedSetDefaultInstall = SpectreAnsiConsole.Confirm(
                        $"Do you want to set the user install path ({resolvedInstallPath}) as the default dotnet install? This will update the PATH and DOTNET_ROOT environment variables.",
                        defaultValue: true);
                }
                else if (defaultInstallState == InstallType.Inconsistent)
                {
                    //  TODO: Figure out what to do here
                    resolvedSetDefaultInstall = false;
                }
            }
            else
            {
                resolvedSetDefaultInstall = false; // Default to not setting the default install path if not specified
            }
        }

        List<string> additionalVersionsToInstall = new();

        // Create a request and resolve it using the channel version resolver
        var installRequest = new DotnetInstallRequest(
            resolvedChannel,
            resolvedInstallPath,
            InstallType.User,
            InstallMode.SDK,
            DnupUtilities.GetInstallArchitecture(RuntimeInformation.ProcessArchitecture),
            new ManagementCadence(ManagementCadenceType.DNUP),
            new InstallRequestOptions());

        var resolvedInstall = _channelVersionResolver.Resolve(installRequest);
        var resolvedChannelVersion = resolvedInstall.FullySpecifiedVersion.Value;

        if (resolvedSetDefaultInstall == true && defaultInstallState == InstallType.Admin)
        {
            if (_interactive)
            {
                var latestAdminVersion = _dotnetInstaller.GetLatestInstalledAdminVersion();
                if (latestAdminVersion != null && new ReleaseVersion(resolvedChannelVersion) < new ReleaseVersion(latestAdminVersion))
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

        SpectreAnsiConsole.MarkupInterpolated($"Installing .NET SDK [blue]{resolvedChannelVersion}[/] to [blue]{resolvedInstallPath}[/]...");

        // Create and use a progress context
        var progressContext = SpectreAnsiConsole.Progress().Start(ctx => ctx);

        // Install the main SDK using the InstallerOrchestratorSingleton directly
        DotnetInstall? mainInstall = InstallerOrchestratorSingleton.Instance.Install(installRequest);
        if (mainInstall == null)
        {
            SpectreAnsiConsole.MarkupLine($"[red]Failed to install .NET SDK {resolvedChannelVersion}[/]");
            return 1;
        }
        SpectreAnsiConsole.MarkupLine($"[green]Installed .NET SDK {mainInstall.FullySpecifiedVersion}, available via {mainInstall.MuxerDirectory}[/]");

        // Install any additional versions
        foreach (var additionalVersion in additionalVersionsToInstall)
        {
            // Create the request for the additional version
            var additionalRequest = new DotnetInstallRequest(
                additionalVersion,
                resolvedInstallPath,
                InstallType.User,
                InstallMode.SDK,
                DnupUtilities.GetInstallArchitecture(RuntimeInformation.ProcessArchitecture),
                new ManagementCadence(ManagementCadenceType.DNUP),
                new InstallRequestOptions());

            // Install the additional version directly using InstallerOrchestratorSingleton
            DotnetInstall? additionalInstall = InstallerOrchestratorSingleton.Instance.Install(additionalRequest);
            if (additionalInstall == null)
            {
                SpectreAnsiConsole.MarkupLine($"[red]Failed to install additional .NET SDK {additionalVersion}[/]");
            }
            else
            {
                SpectreAnsiConsole.MarkupLine($"[green]Installed additional .NET SDK {additionalInstall.FullySpecifiedVersion}, available via {additionalInstall.MuxerDirectory}[/]");
            }
        }

        if (resolvedSetDefaultInstall == true)
        {
            _dotnetInstaller.ConfigureInstallType(InstallType.User, resolvedInstallPath);
        }

        if (resolvedUpdateGlobalJson == true)
        {
            _dotnetInstaller.UpdateGlobalJson(globalJsonInfo!.GlobalJsonPath!, resolvedChannelVersion, globalJsonInfo.AllowPrerelease, globalJsonInfo.RollForward);
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
