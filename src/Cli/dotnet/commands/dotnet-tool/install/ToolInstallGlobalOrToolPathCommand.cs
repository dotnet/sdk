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
        private readonly IReporter _errorReporter;
        private CreateShellShimRepository _createShellShimRepository;
        private readonly CreateToolPackageStoresAndDownloaderAndUninstaller _createToolPackageStoreDownloaderUninstaller;
        private readonly ShellShimTemplateFinder _shellShimTemplateFinder;

        private readonly PackageId _packageId;
        private readonly string _packageVersion;
        private readonly string _configFilePath;
        private readonly string _framework;
        private readonly string[] _source;
        private readonly bool _global;
        private readonly VerbosityOptions _verbosity;
        private readonly string _toolPath;
        private readonly string _architectureOption;
        private IEnumerable<string> _forwardRestoreArguments;
        private readonly bool _allowPackageDowngrade;

        public ToolInstallGlobalOrToolPathCommand(
            ParseResult parseResult,
            CreateToolPackageStoresAndDownloaderAndUninstaller createToolPackageStoreDownloaderUninstaller = null,
            CreateShellShimRepository createShellShimRepository = null,
            IEnvironmentPathInstruction environmentPathInstruction = null,
            IReporter reporter = null,
            INuGetPackageDownloader nugetPackageDownloader = null)
            : base(parseResult)
        {
            _packageId = new PackageId(parseResult.GetValue(ToolInstallCommandParser.PackageIdArgument));
            _packageVersion = parseResult.GetValue(ToolInstallCommandParser.VersionOption);
            _configFilePath = parseResult.GetValue(ToolInstallCommandParser.ConfigOption);
            _framework = parseResult.GetValue(ToolInstallCommandParser.FrameworkOption);
            _source = parseResult.GetValue(ToolInstallCommandParser.AddSourceOption);
            _global = parseResult.GetValue(ToolAppliedOption.GlobalOption);
            _verbosity = parseResult.GetValue(ToolInstallCommandParser.VerbosityOption);
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
            var restoreAction = new RestoreActionConfig(DisableParallel: parseResult.GetValue(ToolCommandRestorePassThroughOptions.DisableParallelOption),
                NoCache: parseResult.GetValue(ToolCommandRestorePassThroughOptions.NoCacheOption),
                IgnoreFailedSources: parseResult.GetValue(ToolCommandRestorePassThroughOptions.IgnoreFailedSourcesOption),
                Interactive: parseResult.GetValue(ToolCommandRestorePassThroughOptions.InteractiveRestoreOption));
            nugetPackageDownloader ??= new NuGetPackageDownloader(tempDir, verboseLogger: new NullLogger(), restoreActionConfig: restoreAction);
            _shellShimTemplateFinder = new ShellShimTemplateFinder(nugetPackageDownloader, tempDir, packageSourceLocation);
            _allowPackageDowngrade = parseResult.GetValue(ToolInstallCommandParser.AllowPackageDowngradeOption);
            _createToolPackageStoreDownloaderUninstaller = createToolPackageStoreDownloaderUninstaller ??
                                                  ToolPackageFactory.CreateToolPackageStoresAndDownloaderAndUninstaller;

            _reporter = (reporter ?? Reporter.Output);
            _errorReporter = (reporter ?? Reporter.Error);
        }

        public override int Execute()
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
             IToolPackageUninstaller toolPackageUninstaller) = _createToolPackageStoreDownloaderUninstaller(toolPath, _forwardRestoreArguments);

            var appHostSourceDirectory = ShellShimTemplateFinder.GetDefaultAppHostSourceDirectory();
            IShellShimRepository shellShimRepository = _createShellShimRepository(appHostSourceDirectory, toolPath);

            IToolPackage oldPackageNullable = GetOldPackage(toolPackageStoreQuery);

            using (var scope = new TransactionScope(
                TransactionScopeOption.Required,
                TimeSpan.Zero))
            {
                if (oldPackageNullable != null)
                {
                    RunWithHandlingUninstallError(() =>
                    {
                        foreach (RestoredCommand command in oldPackageNullable.Commands)
                        {
                            shellShimRepository.RemoveShim(command.Name);
                        }

                        toolPackageUninstaller.Uninstall(oldPackageNullable.PackageDirectory);
                    });
                }

                RunWithHandlingInstallError(() =>
                {
                    IToolPackage newInstalledPackage = toolPackageDownloader.InstallPackage(
                    new PackageLocation(nugetConfig: GetConfigFile(), additionalFeeds: _source),
                        packageId: _packageId,
                        versionRange: versionRange,
                        targetFramework: _framework,
                        verbosity: _verbosity,
                        isGlobalTool: true
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

                    foreach (RestoredCommand command in newInstalledPackage.Commands)
                    {
                        shellShimRepository.CreateShim(command.Executable, command.Name, newInstalledPackage.PackagedShims);
                    }

                    foreach (string w in newInstalledPackage.Warnings)
                    {
                        _reporter.WriteLine(w.Yellow());
                    }
                    if (_global)
                    {
                        _environmentPathInstruction.PrintAddPathInstructionIfPathDoesNotExist();
                    }

                    PrintSuccessMessage(oldPackageNullable, newInstalledPackage);
                });

                scope.Complete();
            }

            return 0;
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

        private void RunWithHandlingInstallError(Action installAction)
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
                    string.Format(Update.LocalizableStrings.UpdateToolFailed, _packageId)
                };
                message.AddRange(
                    InstallToolCommandLowLevelErrorConverter.GetUserFacingMessages(ex, _packageId));


                throw new GracefulException(
                    messages: message,
                    verboseMessages: new[] { ex.ToString() },
                    isUserError: false);
            }
        }

        private void RunWithHandlingUninstallError(Action uninstallAction)
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
                    string.Format(Update.LocalizableStrings.UpdateToolFailed, _packageId)
                };
                message.AddRange(
                    ToolUninstallCommandLowLevelErrorConverter.GetUserFacingMessages(ex, _packageId));

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

        private IToolPackage GetOldPackage(IToolPackageStoreQuery toolPackageStoreQuery)
        {
            IToolPackage oldPackageNullable;
            try
            {
                oldPackageNullable = toolPackageStoreQuery.EnumeratePackageVersions(_packageId).SingleOrDefault();
            }
            catch (InvalidOperationException)
            {
                throw new GracefulException(
                    messages: new[]
                    {
                        string.Format(
                            Update.LocalizableStrings.ToolHasMultipleVersionsInstalled,
                            _packageId),
                    },
                    isUserError: false);
            }

            return oldPackageNullable;
        }

        private void PrintSuccessMessage(IToolPackage oldPackage, IToolPackage newInstalledPackage)
        {
            if (oldPackage == null)
            {
                _reporter.WriteLine(
                    string.Format(
                        Install.LocalizableStrings.InstallationSucceeded,
                        string.Join(", ", newInstalledPackage.Commands.Select(c => c.Name)),
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
