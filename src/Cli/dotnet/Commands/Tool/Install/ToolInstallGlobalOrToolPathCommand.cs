// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Versioning;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.ShellShim;
using Microsoft.DotNet.Cli.Commands.Tool.Update;
using Microsoft.DotNet.Cli.Commands.Tool.Uninstall;
using Microsoft.DotNet.Cli.Commands.Tool.List;

namespace Microsoft.DotNet.Cli.Commands.Tool.Install;

internal delegate IShellShimRepository CreateShellShimRepository(string appHostSourceDirectory, DirectoryPath? nonGlobalLocation = null);

internal delegate (IToolPackageStore, IToolPackageStoreQuery, IToolPackageDownloader) CreateToolPackageStoresAndDownloader(
    DirectoryPath? nonGlobalLocation = null,
    IEnumerable<string>? forwardRestoreArguments = null);

internal class ToolInstallGlobalOrToolPathCommand : CommandBase
{
    private readonly IEnvironmentPathInstruction _environmentPathInstruction;
    private readonly IReporter _reporter;
    private readonly CreateShellShimRepository _createShellShimRepository;
    private readonly CreateToolPackageStoresAndDownloaderAndUninstaller _createToolPackageStoreDownloaderUninstaller;
    private readonly ShellShimTemplateFinder _shellShimTemplateFinder;
    private readonly IToolPackageStoreQuery? _store;

    private readonly PackageId? _packageId;
    private readonly string? _configFilePath;
    private readonly string? _framework;
    private readonly string[]? _source;
    private readonly string[]? _addSource;
    private readonly bool _global;
    private readonly VerbosityOptions _verbosity;
    private readonly string? _toolPath;
    private readonly string? _architectureOption;
    private readonly IEnumerable<string> _forwardRestoreArguments;
    private readonly bool _allowRollForward;
    private readonly bool _allowPackageDowngrade;
    private readonly bool _updateAll;
    private readonly string? _currentWorkingDirectory;
    private readonly bool? _verifySignatures;

    internal readonly RestoreActionConfig _restoreActionConfig;

    public ToolInstallGlobalOrToolPathCommand(
        ParseResult parseResult,
        PackageId? packageId = null,
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

        var packageIdArgument = parseResult.GetValue(ToolInstallCommandParser.PackageIdentityArgument).Id;

        _packageId = packageId ?? (packageIdArgument is not null ? new PackageId(packageIdArgument) : null);
        _configFilePath = parseResult.GetValue(ToolInstallCommandParser.ConfigOption);
        _framework = parseResult.GetValue(ToolInstallCommandParser.FrameworkOption);
        _source = parseResult.GetValue(ToolInstallCommandParser.SourceOption);
        _addSource = parseResult.GetValue(ToolInstallCommandParser.AddSourceOption);
        _global = parseResult.GetValue(ToolInstallCommandParser.GlobalOption);
        _verbosity = GetValueOrDefault(ToolInstallCommandParser.VerbosityOption, VerbosityOptions.minimal, parseResult);
        _toolPath = parseResult.GetValue(ToolInstallCommandParser.ToolPathOption);
        _architectureOption = parseResult.GetValue(ToolInstallCommandParser.ArchitectureOption);

        _forwardRestoreArguments = parseResult.OptionValuesToBeForwarded(ToolInstallCommandParser.GetCommand());

        _environmentPathInstruction = environmentPathInstruction
            ?? EnvironmentPathFactory.CreateEnvironmentPathInstruction();
        _createShellShimRepository = createShellShimRepository ?? ShellShimRepositoryFactory.CreateShellShimRepository;
        var tempDir = new DirectoryPath(PathUtilities.CreateTempSubdirectory());
        var configOption = parseResult.GetValue(ToolInstallCommandParser.ConfigOption);
        var sourceOption = parseResult.GetValue(ToolInstallCommandParser.AddSourceOption);
        var packageSourceLocation = new PackageSourceLocation(string.IsNullOrEmpty(configOption) ? null : new FilePath(configOption), additionalSourceFeeds: sourceOption, basePath: _currentWorkingDirectory);
        _restoreActionConfig = new RestoreActionConfig(DisableParallel: parseResult.GetValue(ToolCommandRestorePassThroughOptions.DisableParallelOption),
            NoCache: parseResult.GetValue(ToolCommandRestorePassThroughOptions.NoCacheOption) || parseResult.GetValue(ToolCommandRestorePassThroughOptions.NoHttpCacheOption),
            IgnoreFailedSources: parseResult.GetValue(ToolCommandRestorePassThroughOptions.IgnoreFailedSourcesOption),
            Interactive: parseResult.GetValue(ToolCommandRestorePassThroughOptions.InteractiveRestoreOption));
        nugetPackageDownloader ??= new NuGetPackageDownloader.NuGetPackageDownloader(tempDir, verboseLogger: new NullLogger(), restoreActionConfig: _restoreActionConfig, verbosityOptions: _verbosity, verifySignatures: verifySignatures ?? true, shouldUsePackageSourceMapping: true);
        _shellShimTemplateFinder = new ShellShimTemplateFinder(nugetPackageDownloader, tempDir, packageSourceLocation);
        _store = store;

        _allowRollForward = parseResult.GetValue(ToolInstallCommandParser.RollForwardOption);

        _allowPackageDowngrade = parseResult.GetValue(ToolInstallCommandParser.AllowPackageDowngradeOption);
        _createToolPackageStoreDownloaderUninstaller = createToolPackageStoreDownloaderUninstaller ??
                                              ToolPackageFactory.CreateToolPackageStoresAndDownloaderAndUninstaller;
        _updateAll = parseResult.GetValue(ToolUpdateCommandParser.UpdateAllOption);

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
            var toolListCommand = new ToolListGlobalOrToolPathCommand(_parseResult, toolPath => { return _store; });
            var toolIds = toolListCommand.GetPackages(null, null);
            foreach (var toolId in toolIds)
            {
                ExecuteInstallCommand(new PackageId(toolId.Id.ToString()));
            }
            return 0;
        }

        if (_packageId is null)
        {
            throw new GracefulException(CliCommandStrings.ToolInstallPackageIdMissing);
        }

        return ExecuteInstallCommand((PackageId)_packageId);
    }

    private int ExecuteInstallCommand(PackageId packageId)
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

        VersionRange? versionRange = _parseResult.GetVersionRange();

        (IToolPackageStore toolPackageStore,
         IToolPackageStoreQuery toolPackageStoreQuery,
         IToolPackageDownloader toolPackageDownloader,
         IToolPackageUninstaller toolPackageUninstaller) = _createToolPackageStoreDownloaderUninstaller(toolPath, _forwardRestoreArguments, _currentWorkingDirectory);

        var appHostSourceDirectory = ShellShimTemplateFinder.GetDefaultAppHostSourceDirectory();
        IShellShimRepository shellShimRepository = _createShellShimRepository(appHostSourceDirectory, toolPath);

        IToolPackage? oldPackageNullable = GetOldPackage(toolPackageStoreQuery, packageId);

        if (oldPackageNullable is not null)
        {
            NuGetVersion nugetVersion = GetBestMatchNugetVersion(packageId, versionRange, toolPackageDownloader);
            _activity?.DisplayName = $"Install {packageId}@{nugetVersion}";
            _activity?.SetTag("tool.package.id", packageId);
            _activity?.SetTag("tool.package.version", nugetVersion);

            if (ToolVersionAlreadyInstalled(oldPackageNullable, nugetVersion))
            {
                _reporter.WriteLine(string.Format(CliCommandStrings.ToolAlreadyInstalled, oldPackageNullable.Id, oldPackageNullable.Version.ToNormalizedString()).Green());
                return 0;
            }
        }

        TransactionalAction.Run(() =>
        {
            if (oldPackageNullable is not null)
            {
                RunWithHandlingUninstallError(() =>
                {
                    shellShimRepository.RemoveShim(oldPackageNullable.Command);
                    toolPackageUninstaller.Uninstall(oldPackageNullable.PackageDirectory);
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
                    restoreActionConfig: _restoreActionConfig
                );
                toolPackageDownloaderActivity?.Dispose();

                EnsureVersionIsHigher(oldPackageNullable, newInstalledPackage, _allowPackageDowngrade);

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
                string appHostSourceDirectory = _shellShimTemplateFinder.ResolveAppHostSourceDirectoryAsync(_architectureOption, framework, RuntimeInformation.ProcessArchitecture).Result;
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

                PrintSuccessMessage(oldPackageNullable, newInstalledPackage);
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
            restoreActionConfig: _restoreActionConfig
        ).version;
    }

    private static bool ToolVersionAlreadyInstalled(IToolPackage? oldPackageNullable, NuGetVersion nuGetVersion)
    {
        return oldPackageNullable != null && oldPackageNullable.Version == nuGetVersion;
    }

    private static void EnsureVersionIsHigher(IToolPackage? oldPackageNullable, IToolPackage newInstalledPackage, bool allowDowngrade)
    {
        if (oldPackageNullable != null && newInstalledPackage.Version < oldPackageNullable.Version && !allowDowngrade)
        {
            throw new GracefulException(
            [
                string.Format(CliCommandStrings.UpdateToLowerVersion,
                    newInstalledPackage.Version.ToNormalizedString(),
                    oldPackageNullable.Version.ToNormalizedString())
            ], isUserError: false);
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

    private static IToolPackage? GetOldPackage(IToolPackageStoreQuery toolPackageStoreQuery, PackageId packageId)
    {
        IToolPackage? oldPackageNullable;
        try
        {
            oldPackageNullable = toolPackageStoreQuery.EnumeratePackageVersions(packageId).SingleOrDefault();
        }
        catch (InvalidOperationException)
        {
            throw new GracefulException(messages:
            [
                string.Format(CliCommandStrings.ToolUpdateToolHasMultipleVersionsInstalled, packageId)
            ], isUserError: false);
        }

        return oldPackageNullable;
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
