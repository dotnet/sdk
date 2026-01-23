// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Install;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

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

        // Check if user wants to migrate admin versions when switching to user install
        var additionalVersionsToInstall = InstallWalkthrough.GetAdditionalAdminVersionsToMigrate(
            resolvedVersion,
            resolvedSetDefaultInstall,
            currentDotnetInstallRoot,
            _interactive,
            ComponentDescription);

        //  TODO: Implement transaction / rollback?

        var installResult = InstallExecutor.ExecuteInstall(installRequest, resolvedVersion?.ToString(), ComponentDescription, _noProgress);
        if (!installResult.Success)
        {
            return 1;
        }

        // Install any additional versions
        InstallExecutor.ExecuteAdditionalInstalls(
            additionalVersionsToInstall,
            new DotnetInstallRoot(resolvedInstallPath, InstallerUtilities.GetDefaultInstallArchitecture()),
            InstallComponent.SDK,
            ComponentDescription,
            _manifestPath,
            _noProgress);

        InstallExecutor.ConfigureDefaultInstallIfRequested(_dotnetInstaller, resolvedSetDefaultInstall, resolvedInstallPath);

        if (resolvedUpdateGlobalJson == true)
        {
            _dotnetInstaller.UpdateGlobalJson(globalJsonInfo!.GlobalJsonPath!, resolvedVersion!.ToString(), globalJsonInfo.AllowPrerelease, globalJsonInfo.RollForward);
        }

        InstallExecutor.DisplayComplete();

        return 0;
    }



    string? ResolveChannelFromGlobalJson(string globalJsonPath)
    {
        //return null;
        //return "9.0";
        return Environment.GetEnvironmentVariable("DOTNET_TESTHOOK_GLOBALJSON_SDK_CHANNEL");
    }

}
