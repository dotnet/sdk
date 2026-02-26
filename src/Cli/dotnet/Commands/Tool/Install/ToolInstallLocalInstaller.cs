// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.Commands.Tool.Install;

internal sealed class ToolInstallLocalInstaller(
    string? configFilePath,
    string[]? sources,
    VerbosityOptions verbosity,
    IToolPackageDownloader? toolPackageDownloader = null,
    string? runtimeJsonPathForTests = null,
    RestoreActionConfig? restoreActionConfig = null)
{
    public readonly string TargetFrameworkToInstall = BundledTargetFramework.GetTargetFrameworkMoniker();

    private readonly IToolPackageDownloader _toolPackageDownloader = toolPackageDownloader
        ?? ToolPackageFactory.CreateToolPackageStoresAndDownloader(runtimeJsonPathForTests: runtimeJsonPathForTests).downloader;

    public IToolPackage Install(FilePath manifestFile, PackageId packageId, VersionRange? versionRange)
    {
        if (!string.IsNullOrEmpty(configFilePath) && !File.Exists(configFilePath))
        {
            throw new GracefulException(
                string.Format(
                    CliCommandStrings.ToolInstallNuGetConfigurationFileDoesNotExist,
                    Path.GetFullPath(configFilePath)));
        }

        FilePath? configFile = null;
        if (!string.IsNullOrEmpty(configFilePath))
        {
            configFile = new FilePath(configFilePath);
        }

        try
        {
            //  NOTE: The manifest file may or may not be under a .config folder.  If it is, we will use
            //  that directory as the root config directory.  This should be OK, as usually there won't be
            //  a NuGet.config in the .config folder, and if there is it's better to use it than to go one
            //  more level up and miss the root repo folder if the manifest file is not under a .config folder.
            var rootConfigDirectory = manifestFile.GetDirectoryPath();

            IToolPackage toolDownloadedPackage = _toolPackageDownloader.InstallPackage(
                    new PackageLocation(
                        nugetConfig: configFile,
                        additionalFeeds: sources,
                        rootConfigDirectory: rootConfigDirectory),
                    packageId,
                    verbosity: verbosity,
                    versionRange,
                    TargetFrameworkToInstall,
                    restoreActionConfig: restoreActionConfig
                    );

            return toolDownloadedPackage;
        }
        catch (Exception ex) when (InstallToolCommandLowLevelErrorConverter.ShouldConvertToUserFacingError(ex))
        {
            throw new GracefulException(
                messages: InstallToolCommandLowLevelErrorConverter.GetUserFacingMessages(ex, packageId),
                verboseMessages: [ex.ToString()],
                isUserError: false);
        }
    }
}
