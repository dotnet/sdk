// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Diagnostics;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Tool.List;
using Microsoft.DotNet.Cli.Commands.Tool.Uninstall;
using Microsoft.DotNet.Cli.Commands.Tool.Update;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.ShellShim;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.Commands.Tool.Install;

internal delegate IShellShimRepository CreateShellShimRepository(string appHostSourceDirectory, DirectoryPath? nonGlobalLocation = null);

internal delegate (IToolPackageStore, IToolPackageStoreQuery, IToolPackageDownloader) CreateToolPackageStoresAndDownloader(
    DirectoryPath? nonGlobalLocation = null,
    IEnumerable<string>? forwardRestoreArguments = null);

internal sealed class ToolInstallGlobalOrToolPathCommand : CommandBase<ToolUpdateInstallCommandDefinition>
{
    private readonly IEnvironmentPathInstruction _environmentPathInstruction;
    private readonly IReporter _reporter;
    private readonly CreateShellShimRepository _createShellShimRepository;
    private readonly CreateToolPackageStoresAndDownloaderAndUninstaller _createToolPackageStoreDownloaderUninstaller;
    private readonly ShellShimTemplateFinder _shellShimTemplateFinder;
    private readonly IToolPackageStoreQuery? _store;

    private readonly string? _configFilePath;
    private readonly string? _framework;
    private readonly string[]? _source;
    private readonly string[]? _addSource;
    private readonly bool _global;
    private readonly VerbosityOptions _verbosity;
    private readonly string? _toolPath;
    private readonly string? _architecture;
    private readonly PackageIdentityWithRange? _packageIdentityWithRange;
    private readonly IEnumerable<string> _forwardRestoreArguments;
    private readonly bool _allowRollForward;
    private readonly bool _allowPackageDowngrade;
    private readonly bool _updateAll;
    private readonly string? _currentWorkingDirectory;
    private readonly bool? _verifySignatures;

    internal readonly RestoreActionConfig restoreActionConfig;

    public ToolInstallGlobalOrToolPathCommand(
        ParseResult parseResult,
        CreateToolPackageStoresAndDownloaderAndUninstaller? createToolPackageStoreDownloaderUninstaller = null,
        CreateShellShimRepository? createShellShimRepository = null,
        IEnvironmentPathInstruction? environmentPathInstruction = null,
        IReporter? reporter = null,
        INuGetPackageDownloader? nugetPackageDownloader = null,
        IToolPackageStoreQuery? store = null,
        string? currentWorkingDirectory = null,
        bool? verifySignatures = null)
        : base(parseResult)
    {
        _verifySignatures = verifySignatures;
        _currentWorkingDirectory = currentWorkingDirectory;

        _configFilePath = parseResult.GetValue(Definition.ConfigOption);
        _framework = parseResult.GetValue(Definition.FrameworkOption);
        _source = parseResult.GetValue(Definition.SourceOption);
        _addSource = parseResult.GetValue(Definition.AddSourceOption);
        _global = parseResult.GetValue(Definition.LocationOptions.GlobalOption);
        _verbosity = GetValueOrDefault(Definition.VerbosityOption, VerbosityOptions.minimal, parseResult);
        _toolPath = parseResult.GetValue(Definition.LocationOptions.ToolPathOption);

        if (Definition is ToolUpdateCommandDefinition updateDef)
        {
            _updateAll = parseResult.GetValue(updateDef.UpdateAllOption);
            _packageIdentityWithRange = parseResult.GetValue(updateDef.PackageIdentityArgument);
        }
        else
        {
            var installDef = (ToolInstallCommandDefinition)Definition;
            _packageIdentityWithRange = parseResult.GetValue(installDef.PackageIdentityArgument);
            _architecture = parseResult.GetValue(installDef.ArchitectureOption);
            _allowRollForward = parseResult.GetValue(installDef.RollForwardOption);
        }

        _forwardRestoreArguments = parseResult.OptionValuesToBeForwarded(Definition.Options);

        _environmentPathInstruction = environmentPathInstruction ?? EnvironmentPathFactory.CreateEnvironmentPathInstruction();
        _createShellShimRepository = createShellShimRepository ?? ShellShimRepositoryFactory.CreateShellShimRepository;

        var tempDir = new DirectoryPath(TemporaryDirectory.CreateSubdirectory());
        var configOption = parseResult.GetValue(Definition.ConfigOption);
        var sourceOption = parseResult.GetValue(Definition.AddSourceOption);
        var packageSourceLocation = new PackageSourceLocation(string.IsNullOrEmpty(configOption) ? null : new FilePath(configOption), additionalSourceFeeds: sourceOption, basePath: _currentWorkingDirectory);

        restoreActionConfig = Definition.RestoreOptions.ToRestoreActionConfig(parseResult);

        nugetPackageDownloader ??= new NuGetPackageDownloader.NuGetPackageDownloader(tempDir, verboseLogger: new NullLogger(), restoreActionConfig: restoreActionConfig, verbosityOptions: _verbosity, verifySignatures: verifySignatures ?? true, shouldUsePackageSourceMapping: true, currentWorkingDirectory: _currentWorkingDirectory);

        // Perform HTTP source validation early to ensure compatibility with .NET 9 requirements
        if (_packageIdentityWithRange != null)
        {
            var packageSourceLocationForValidation = new PackageSourceLocation(
                nugetConfig: GetConfigFile(),
                additionalSourceFeeds: _addSource,
                basePath: _currentWorkingDirectory);

            if (nugetPackageDownloader is NuGetPackageDownloader.NuGetPackageDownloader concreteDownloader)
            {
                concreteDownloader.LoadNuGetSources(new PackageId(_packageIdentityWithRange.Value.Id), packageSourceLocationForValidation);
            }
        }

        _shellShimTemplateFinder = new ShellShimTemplateFinder(nugetPackageDownloader, tempDir, packageSourceLocation);
        _store = store;

        _allowPackageDowngrade = parseResult.GetValue(Definition.AllowPackageDowngradeOption);
        _createToolPackageStoreDownloaderUninstaller = createToolPackageStoreDownloaderUninstaller ??
                                              ToolPackageFactory.CreateToolPackageStoresAndDownloaderAndUninstaller;

        _reporter = reporter ?? Reporter.Output;
    }

    public static T GetValueOrDefault<T>(Option<T> option, T defaultOption, ParseResult parseResult)
    {
        if (parseResult.GetResult(option) is { } result &&
            result.GetValueOrDefault<T>() is { } t)
        {
            return t;
        }

        return defaultOption;
    }

    public override int Execute()
    {
        if (_updateAll)
        {
            Debug.Assert(_store != null);

            var toolIds = _store.EnumeratePackages()
                .Where(p => ToolListGlobalOrToolPathCommand.PackageHasCommand(p, Reporter.Output))
                .OrderBy(p => p.Id);

            foreach (var toolId in toolIds)
            {
                ExecuteInstallCommand(new PackageId(toolId.Id.ToString()), versionRange: null);
            }
            return 0;
        }

        // Either --all or package id must be specified:
        Debug.Assert(_packageIdentityWithRange.HasValue);

        var versionRange = VersionRangeUtilities.GetVersionRange(
            _packageIdentityWithRange.Value.VersionRange?.OriginalString,
            _parseResult.GetValue(Definition.VersionOption),
            _parseResult.GetValue(Definition.PrereleaseOption));

        return ExecuteInstallCommand(new PackageId(_packageIdentityWithRange.Value.Id), versionRange);
    }

    private int ExecuteInstallCommand(PackageId packageId, VersionRange? versionRange)
    {
        using var _activity = Activities.Source.StartActivity("install-tool");
        _activity?.DisplayName = $"Install {packageId}";
        _activity?.SetTag("tool.package.id", packageId);

        if (!string.IsNullOrEmpty(_configFilePath) && !File.Exists(_configFilePath))
        {
            throw new GracefulException(string.Format(CliCommandStrings.ToolInstallNuGetConfigurationFileDoesNotExist, Path.GetFullPath(_configFilePath)));
        }

        DirectoryPath? toolPath = null;
        if (!string.IsNullOrEmpty(_toolPath))
        {
            toolPath = new DirectoryPath(_toolPath);
        }

        (IToolPackageStore toolPackageStore,
         IToolPackageStoreQuery toolPackageStoreQuery,
         IToolPackageDownloader toolPackageDownloader,
         IToolPackageUninstaller toolPackageUninstaller) = _createToolPackageStoreDownloaderUninstaller(toolPath, _forwardRestoreArguments, _currentWorkingDirectory);

        var appHostSourceDirectory = ShellShimTemplateFinder.GetDefaultAppHostSourceDirectory();
        IShellShimRepository shellShimRepository = _createShellShimRepository(appHostSourceDirectory, toolPath);

        var oldPackage = TryGetOldPackage(toolPackageStoreQuery, packageId);

        if (oldPackage != null)
        {
            NuGetVersion nugetVersion = GetBestMatchNugetVersion(packageId, versionRange, toolPackageDownloader);
            _activity?.DisplayName = $"Install {packageId}@{nugetVersion}";
            _activity?.SetTag("tool.package.id", packageId);
            _activity?.SetTag("tool.package.version", nugetVersion);

            if (ToolVersionAlreadyInstalled(oldPackage, nugetVersion))
            {
                _reporter.WriteLine(string.Format(CliCommandStrings.ToolAlreadyInstalled, oldPackage.Id, oldPackage.Version.ToNormalizedString()).Green());
                return 0;
            }
        }

        TransactionalAction.Run(() =>
        {
            if (oldPackage != null)
            {
                RunWithHandlingUninstallError(() =>
                {
                    shellShimRepository.RemoveShim(oldPackage.Command);
                    toolPackageUninstaller.Uninstall(oldPackage.PackageDirectory);
                }, packageId);
            }

            RunWithHandlingInstallError(() =>
            {
                var toolPackageDownloaderActivity = Activities.Source.StartActivity("download-tool-package");
                IToolPackage newInstalledPackage = toolPackageDownloader.InstallPackage(
                    new PackageLocation(nugetConfig: GetConfigFile(), sourceFeedOverrides: _source, additionalFeeds: _addSource),
                    packageId: packageId,
                    versionRange: versionRange,
                    targetFramework: _framework,
                    verbosity: _verbosity,
                    isGlobalTool: true,
                    isGlobalToolRollForward: _allowRollForward,
                    verifySignatures: _verifySignatures ?? true,
                    restoreActionConfig: restoreActionConfig);

                EnsureVersionIsHigher(oldPackage, newInstalledPackage, _allowPackageDowngrade);

                NuGetFramework? framework;
                if (string.IsNullOrEmpty(_framework) && newInstalledPackage.Frameworks.Count() > 0)
                {
                    framework = newInstalledPackage.Frameworks
                        .Where(f => f.Version < new NuGetVersion(Product.Version).Version)
                        .MaxBy(f => f.Version);
                }
                else
                {
                    framework = string.IsNullOrEmpty(_framework) ? null : NuGetFramework.Parse(_framework);
                }
                var shimActivity = Activities.Source.StartActivity("create-shell-shim");
                string appHostSourceDirectory = _shellShimTemplateFinder.ResolveAppHostSourceDirectoryAsync(_architecture, framework, RuntimeInformation.ProcessArchitecture).Result;

                shellShimRepository.CreateShim(newInstalledPackage.Command, newInstalledPackage.PackagedShims);
                shimActivity?.Dispose();

                foreach (string w in newInstalledPackage.Warnings)
                {
                    _reporter.WriteLine(w.Yellow());
                }
                if (_global)
                {
                    _environmentPathInstruction.PrintAddPathInstructionIfPathDoesNotExist();
                }

                PrintSuccessMessage(oldPackage, newInstalledPackage);
            }, packageId);
        });

        return 0;
    }

    private NuGetVersion GetBestMatchNugetVersion(PackageId packageId, VersionRange? versionRange, IToolPackageDownloader toolPackageDownloader)
    {
        return toolPackageDownloader.GetNuGetVersion(
            packageLocation: new PackageLocation(nugetConfig: GetConfigFile(), sourceFeedOverrides: _source, additionalFeeds: _addSource),
            packageId: packageId,
            versionRange: versionRange,
            verbosity: _verbosity,
            restoreActionConfig: restoreActionConfig
        ).version;
    }

    private static bool ToolVersionAlreadyInstalled(IToolPackage? oldPackageNullable, NuGetVersion nuGetVersion)
    {
        return oldPackageNullable != null && oldPackageNullable.Version == nuGetVersion;
    }

    private static void EnsureVersionIsHigher(IToolPackage? oldPackage, IToolPackage newInstalledPackage, bool allowDowngrade)
    {
        if (oldPackage != null && newInstalledPackage.Version < oldPackage.Version && !allowDowngrade)
        {
            throw new GracefulException(
                [
                    string.Format(CliCommandStrings.UpdateToLowerVersion,
                        newInstalledPackage.Version.ToNormalizedString(),
                        oldPackage.Version.ToNormalizedString())
                ],
                isUserError: false);
        }
    }

    private void ValidateArguments()
    {
        if (!string.IsNullOrEmpty(_configFilePath) && !File.Exists(_configFilePath))
        {
            throw new GracefulException(
                string.Format(
                    CliCommandStrings.ToolInstallNuGetConfigurationFileDoesNotExist,
                    Path.GetFullPath(_configFilePath)));
        }
    }

    private static void RunWithHandlingInstallError(Action installAction, PackageId packageId)
    {
        try
        {
            installAction();
        }
        catch (Exception ex)
            when (InstallToolCommandLowLevelErrorConverter.ShouldConvertToUserFacingError(ex))
        {
            var message = new List<string>
            {
                string.Format(CliCommandStrings.UpdateToolFailed, packageId)
            };
            message.AddRange(InstallToolCommandLowLevelErrorConverter.GetUserFacingMessages(ex, packageId));

            throw new GracefulException(
                messages: message,
                verboseMessages: [ex.ToString()],
                isUserError: false);
        }
    }

    private static void RunWithHandlingUninstallError(Action uninstallAction, PackageId packageId)
    {
        try
        {
            uninstallAction();
        }
        catch (Exception ex)
            when (ToolUninstallCommandLowLevelErrorConverter.ShouldConvertToUserFacingError(ex))
        {
            var message = new List<string>
            {
                string.Format(CliCommandStrings.UpdateToolFailed, packageId)
            };
            message.AddRange(ToolUninstallCommandLowLevelErrorConverter.GetUserFacingMessages(ex, packageId));

            throw new GracefulException(
                messages: message,
                verboseMessages: [ex.ToString()],
                isUserError: false);
        }
    }

    private FilePath? GetConfigFile()
    {
        FilePath? configFile = null;
        if (!string.IsNullOrEmpty(_configFilePath))
        {
            configFile = new FilePath(_configFilePath);
        }

        return configFile;
    }

    private static IToolPackage? TryGetOldPackage(IToolPackageStoreQuery toolPackageStoreQuery, PackageId packageId)
    {
        try
        {
            return toolPackageStoreQuery.EnumeratePackageVersions(packageId).SingleOrDefault();
        }
        catch (InvalidOperationException)
        {
            throw new GracefulException(messages:
            [
                string.Format(CliCommandStrings.ToolUpdateToolHasMultipleVersionsInstalled, packageId)
            ], isUserError: false);
        }
    }

    private void PrintSuccessMessage(IToolPackage? oldPackage, IToolPackage newInstalledPackage)
    {
        if (!_verbosity.IsQuiet())
        {
            if (oldPackage == null)
            {
                _reporter.WriteLine(
                    string.Format(
                        CliCommandStrings.ToolInstallInstallationSucceeded,
                        newInstalledPackage.Command.Name,
                        newInstalledPackage.Id,
                        newInstalledPackage.Version.ToNormalizedString()).Green());
            }
            else if (oldPackage.Version != newInstalledPackage.Version)
            {
                _reporter.WriteLine(
                    string.Format(
                        CliCommandStrings.ToolUpdateUpdateSucceeded,
                        newInstalledPackage.Id,
                        oldPackage.Version.ToNormalizedString(),
                        newInstalledPackage.Version.ToNormalizedString()).Green());
            }
            else
            {
                _reporter.WriteLine(
                    string.Format(

                        newInstalledPackage.Version.IsPrerelease ?
                        CliCommandStrings.UpdateSucceededPreVersionNoChange : CliCommandStrings.UpdateSucceededStableVersionNoChange,
                        newInstalledPackage.Id, newInstalledPackage.Version).Green());
            }
        }
    }
}
