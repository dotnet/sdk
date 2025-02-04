// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Transactions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ShellShim;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tool.Common;
using Microsoft.DotNet.Tools.Tool.Uninstall;
using Microsoft.DotNet.Tools.Tool.Update;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Versioning;
using Microsoft.DotNet.Tools.Tool.List;

namespace Microsoft.DotNet.Tools.Tool.Install
{
    internal delegate IShellShimRepository CreateShellShimRepository(string appHostSourceDirectory, DirectoryPath? nonGlobalLocation = null);
    
    internal delegate (IToolPackageStore, IToolPackageStoreQuery, IToolPackageDownloader) CreateToolPackageStoresAndDownloader(
        DirectoryPath? nonGlobalLocation = null,
        IEnumerable<string> forwardRestoreArguments = null);

    internal class ToolInstallGlobalOrToolPathCommand : CommandBase
    {
        private readonly IEnvironmentPathInstruction _environmentPathInstruction;
        private readonly IReporter _reporter;
        private CreateShellShimRepository _createShellShimRepository;
        private readonly CreateToolPackageStoresAndDownloaderAndUninstaller _createToolPackageStoreDownloaderUninstaller;
        private readonly ShellShimTemplateFinder _shellShimTemplateFinder;
        private readonly IToolPackageStoreQuery _store;

        private readonly PackageId? _packageId;
        private readonly string _configFilePath;
        private readonly string _framework;
        private readonly string[] _source;
        private readonly string[] _addSource;
        private readonly bool _global;
        private readonly VerbosityOptions _verbosity;
        private readonly string _toolPath;
        private readonly string _architectureOption;
        private IEnumerable<string> _forwardRestoreArguments;
        private readonly bool _allowRollForward;
        private readonly bool _allowPackageDowngrade;
        private readonly bool _updateAll;
        private readonly string _currentWorkingDirectory;
        private readonly bool? _verifySignatures;

        internal readonly RestoreActionConfig restoreActionConfig;

        public ToolInstallGlobalOrToolPathCommand(
            ParseResult parseResult,
            PackageId? packageId = null,
            CreateToolPackageStoresAndDownloaderAndUninstaller createToolPackageStoreDownloaderUninstaller = null,
            CreateShellShimRepository createShellShimRepository = null,
            IEnvironmentPathInstruction environmentPathInstruction = null,
            IReporter reporter = null,
            INuGetPackageDownloader nugetPackageDownloader = null,
            IToolPackageStoreQuery store = null,
            string currentWorkingDirectory = null,
            bool? verifySignatures = null)
            : base(parseResult)
        {
            _verifySignatures = verifySignatures;
            _currentWorkingDirectory = currentWorkingDirectory;
            var packageIdArgument = parseResult.GetValue(ToolInstallCommandParser.PackageIdArgument);
            _packageId = packageId ?? (packageIdArgument is not null ? new PackageId(packageIdArgument) : null);
            _configFilePath = parseResult.GetValue(ToolInstallCommandParser.ConfigOption);
            _framework = parseResult.GetValue(ToolInstallCommandParser.FrameworkOption);
            _source = parseResult.GetValue(ToolInstallCommandParser.SourceOption);
            _addSource = parseResult.GetValue(ToolInstallCommandParser.AddSourceOption);
            _global = parseResult.GetValue(ToolAppliedOption.GlobalOption);
            _verbosity = GetValueOrDefault(ToolInstallCommandParser.VerbosityOption, VerbosityOptions.minimal, parseResult);
            _toolPath = parseResult.GetValue(ToolAppliedOption.ToolPathOption);
            _architectureOption = parseResult.GetValue(ToolInstallCommandParser.ArchitectureOption);

            _forwardRestoreArguments = parseResult.OptionValuesToBeForwarded(ToolInstallCommandParser.GetCommand());

            _environmentPathInstruction = environmentPathInstruction
                ?? EnvironmentPathFactory.CreateEnvironmentPathInstruction();
            _createShellShimRepository = createShellShimRepository ?? ShellShimRepositoryFactory.CreateShellShimRepository;
            var tempDir = new DirectoryPath(PathUtilities.CreateTempSubdirectory());
            var configOption = parseResult.GetValue(ToolInstallCommandParser.ConfigOption);
            var sourceOption = parseResult.GetValue(ToolInstallCommandParser.AddSourceOption);
            var packageSourceLocation = new PackageSourceLocation(string.IsNullOrEmpty(configOption) ? null : new FilePath(configOption), additionalSourceFeeds: sourceOption);
            restoreActionConfig = new RestoreActionConfig(DisableParallel: parseResult.GetValue(ToolCommandRestorePassThroughOptions.DisableParallelOption),
                NoCache: (parseResult.GetValue(ToolCommandRestorePassThroughOptions.NoCacheOption) || parseResult.GetValue(ToolCommandRestorePassThroughOptions.NoHttpCacheOption)),
                IgnoreFailedSources: parseResult.GetValue(ToolCommandRestorePassThroughOptions.IgnoreFailedSourcesOption),
                Interactive: parseResult.GetValue(ToolCommandRestorePassThroughOptions.InteractiveRestoreOption));
            nugetPackageDownloader ??= new NuGetPackageDownloader(tempDir, verboseLogger: new NullLogger(), restoreActionConfig: restoreActionConfig, verbosityOptions: _verbosity, verifySignatures: verifySignatures ?? true);
            _shellShimTemplateFinder = new ShellShimTemplateFinder(nugetPackageDownloader, tempDir, packageSourceLocation);
            _store = store;

            _allowRollForward = parseResult.GetValue(ToolInstallCommandParser.RollForwardOption);

            _allowPackageDowngrade = parseResult.GetValue(ToolInstallCommandParser.AllowPackageDowngradeOption);
            _createToolPackageStoreDownloaderUninstaller = createToolPackageStoreDownloaderUninstaller ??
                                                  ToolPackageFactory.CreateToolPackageStoresAndDownloaderAndUninstaller;
            _updateAll = parseResult.GetValue(ToolUpdateCommandParser.UpdateAllOption);

            _reporter = (reporter ?? Reporter.Output);
        }

        public static T GetValueOrDefault<T>(CliOption<T> option, T defaultOption, ParseResult parseResult)
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
                var toolListCommand = new ToolListGlobalOrToolPathCommand(
                    _parseResult
                    , toolPath => { return _store; }
                    );
                var toolIds = toolListCommand.GetPackages(null, null);
                foreach (var toolId in toolIds)
                {
                    ExecuteInstallCommand(new PackageId(toolId.Id.ToString()));
                }
                return 0;
            }
            else
            {
                return ExecuteInstallCommand((PackageId)_packageId);
            }
        }

        private int ExecuteInstallCommand(PackageId packageId)
        {
            ValidateArguments();

            DirectoryPath? toolPath = null;
            if (!string.IsNullOrEmpty(_toolPath))
            {
                toolPath = new DirectoryPath(_toolPath);
            }

            VersionRange versionRange = _parseResult.GetVersionRange();

            (IToolPackageStore toolPackageStore,
             IToolPackageStoreQuery toolPackageStoreQuery,
             IToolPackageDownloader toolPackageDownloader,
             IToolPackageUninstaller toolPackageUninstaller) = _createToolPackageStoreDownloaderUninstaller(toolPath, _forwardRestoreArguments, _currentWorkingDirectory);

            var appHostSourceDirectory = ShellShimTemplateFinder.GetDefaultAppHostSourceDirectory();
            IShellShimRepository shellShimRepository = _createShellShimRepository(appHostSourceDirectory, toolPath);

            IToolPackage oldPackageNullable = GetOldPackage(toolPackageStoreQuery, packageId);

            if (oldPackageNullable != null)
            {
                NuGetVersion nugetVersion = GetBestMatchNugetVersion(packageId, versionRange, toolPackageDownloader);

                if (ToolVersionAlreadyInstalled(oldPackageNullable, nugetVersion))
                {
                    _reporter.WriteLine(string.Format(LocalizableStrings.ToolAlreadyInstalled, oldPackageNullable.Id, oldPackageNullable.Version.ToNormalizedString()).Green());
                    return 0;
                }   
            }

            using (var scope = new TransactionScope(
                TransactionScopeOption.Required,
                TimeSpan.Zero))
            {
                if (oldPackageNullable != null)
                {
                    RunWithHandlingUninstallError(() =>
                    {
                        shellShimRepository.RemoveShim(oldPackageNullable.Command.Name);
                        toolPackageUninstaller.Uninstall(oldPackageNullable.PackageDirectory);
                    }, packageId);
                }

                RunWithHandlingInstallError(() =>
                {
                    IToolPackage newInstalledPackage = toolPackageDownloader.InstallPackage(
                    new PackageLocation(nugetConfig: GetConfigFile(), sourceFeedOverrides: _source, additionalFeeds: _addSource),
                        packageId: packageId,
                        versionRange: versionRange,
                        targetFramework: _framework,
                        verbosity: _verbosity,
                        isGlobalTool: true,
                        isGlobalToolRollForward: _allowRollForward,
                        verifySignatures: _verifySignatures ?? true,
                        restoreActionConfig: restoreActionConfig
                    );

                    EnsureVersionIsHigher(oldPackageNullable, newInstalledPackage, _allowPackageDowngrade);

                    NuGetFramework framework;
                    if (string.IsNullOrEmpty(_framework) && newInstalledPackage.Frameworks.Count() > 0)
                    {
                        framework = newInstalledPackage.Frameworks
                            .Where(f => f.Version < (new NuGetVersion(Product.Version)).Version)
                            .MaxBy(f => f.Version);
                    }
                    else
                    {
                        framework = string.IsNullOrEmpty(_framework) ?
                            null :
                            NuGetFramework.Parse(_framework);
                    }
                    string appHostSourceDirectory = _shellShimTemplateFinder.ResolveAppHostSourceDirectoryAsync(_architectureOption, framework, RuntimeInformation.ProcessArchitecture).Result;

                    shellShimRepository.CreateShim(newInstalledPackage.Command.Executable, newInstalledPackage.Command.Name, newInstalledPackage.PackagedShims);

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

                scope.Complete();

            }
            return 0;
        }

        private NuGetVersion GetBestMatchNugetVersion(PackageId packageId, VersionRange versionRange, IToolPackageDownloader toolPackageDownloader)
        {
            return toolPackageDownloader.GetNuGetVersion(
                packageLocation: new PackageLocation(nugetConfig: GetConfigFile(), sourceFeedOverrides: _source, additionalFeeds: _addSource),
                packageId: packageId,
                versionRange: versionRange,
                verbosity: _verbosity,
                isGlobalTool: true,
                restoreActionConfig: restoreActionConfig
            );
        }

        private static bool ToolVersionAlreadyInstalled(IToolPackage oldPackageNullable, NuGetVersion nuGetVersion)
        {
            return oldPackageNullable != null && (oldPackageNullable.Version == nuGetVersion);
        }

        private static void EnsureVersionIsHigher(IToolPackage oldPackageNullable, IToolPackage newInstalledPackage, bool allowDowngrade)
        {
            if (oldPackageNullable != null && (newInstalledPackage.Version < oldPackageNullable.Version && !allowDowngrade))
            {
                throw new GracefulException(
                    new[]
                    {
                        string.Format(Update.LocalizableStrings.UpdateToLowerVersion,
                            newInstalledPackage.Version.ToNormalizedString(),
                            oldPackageNullable.Version.ToNormalizedString())
                    },
                    isUserError: false);
            }
        }

        private void ValidateArguments()
        {
            if (!string.IsNullOrEmpty(_configFilePath) && !File.Exists(_configFilePath))
            {
                throw new GracefulException(
                    string.Format(
                        LocalizableStrings.NuGetConfigurationFileDoesNotExist,
                        Path.GetFullPath(_configFilePath)));
            }
        }

        private void RunWithHandlingInstallError(Action installAction, PackageId packageId)
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
                    string.Format(Update.LocalizableStrings.UpdateToolFailed, packageId)
                };
                message.AddRange(
                    InstallToolCommandLowLevelErrorConverter.GetUserFacingMessages(ex, packageId));


                throw new GracefulException(
                    messages: message,
                    verboseMessages: new[] { ex.ToString() },
                    isUserError: false);
            }
        }

        private void RunWithHandlingUninstallError(Action uninstallAction, PackageId packageId)
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
                    string.Format(Update.LocalizableStrings.UpdateToolFailed, packageId)
                };
                message.AddRange(
                    ToolUninstallCommandLowLevelErrorConverter.GetUserFacingMessages(ex, packageId));

                throw new GracefulException(
                    messages: message,
                    verboseMessages: new[] { ex.ToString() },
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

        private IToolPackage GetOldPackage(IToolPackageStoreQuery toolPackageStoreQuery, PackageId packageId)
        {
            IToolPackage oldPackageNullable;
            try
            {
                oldPackageNullable = toolPackageStoreQuery.EnumeratePackageVersions(packageId).SingleOrDefault();
            }
            catch (InvalidOperationException)
            {
                throw new GracefulException(
                    messages: new[]
                    {
                        string.Format(
                            Update.LocalizableStrings.ToolHasMultipleVersionsInstalled,
                            packageId),
                    },
                    isUserError: false);
            }

            return oldPackageNullable;
        }

        private void PrintSuccessMessage(IToolPackage oldPackage, IToolPackage newInstalledPackage)
        {
            if (!_verbosity.IsQuiet())
            {
                if (oldPackage == null)
                {
                    _reporter.WriteLine(
                        string.Format(
                            LocalizableStrings.InstallationSucceeded,
                            newInstalledPackage.Command.Name,
                            newInstalledPackage.Id,
                            newInstalledPackage.Version.ToNormalizedString()).Green());
                }
                else if (oldPackage.Version != newInstalledPackage.Version)
                {
                    _reporter.WriteLine(
                        string.Format(
                            Update.LocalizableStrings.UpdateSucceeded,
                            newInstalledPackage.Id,
                            oldPackage.Version.ToNormalizedString(),
                            newInstalledPackage.Version.ToNormalizedString()).Green());
                }
                else
                {
                    _reporter.WriteLine(
                        string.Format(
                            (
                            newInstalledPackage.Version.IsPrerelease ?
                            Update.LocalizableStrings.UpdateSucceededPreVersionNoChange : Update.LocalizableStrings.UpdateSucceededStableVersionNoChange
                            ),
                            newInstalledPackage.Id, newInstalledPackage.Version).Green());
                }
            }
        }
    }
}
