// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.ToolManifest;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.Commands.Tool.Restore;

internal class ToolRestoreCommand : CommandBase
{
    private readonly string _configFilePath;
    private readonly IReporter _errorReporter;
    private readonly ILocalToolsResolverCache _localToolsResolverCache;
    private readonly IToolManifestFinder _toolManifestFinder;
    private readonly IFileSystem _fileSystem;
    private readonly IReporter _reporter;
    private readonly string[] _sources;
    private readonly IToolPackageDownloader _toolPackageDownloader;
    private readonly VerbosityOptions _verbosity;
    private readonly RestoreActionConfig _restoreActionConfig;

    public ToolRestoreCommand(
        ParseResult result,
        IToolPackageDownloader toolPackageDownloader = null,
        IToolManifestFinder toolManifestFinder = null,
        ILocalToolsResolverCache localToolsResolverCache = null,
        IFileSystem fileSystem = null,
        IReporter reporter = null)
        : base(result)
    {
        if (toolPackageDownloader == null)
        {
            (IToolPackageStore,
                IToolPackageStoreQuery,
                IToolPackageDownloader downloader) toolPackageStoresAndInstaller
                    = ToolPackageFactory.CreateToolPackageStoresAndDownloader();
            _toolPackageDownloader = toolPackageStoresAndInstaller.downloader;
        }
        else
        {
            _toolPackageDownloader = toolPackageDownloader;
        }

        _toolManifestFinder
            = toolManifestFinder
              ?? new ToolManifestFinder(new DirectoryPath(Directory.GetCurrentDirectory()));

        _localToolsResolverCache = localToolsResolverCache ?? new LocalToolsResolverCache();
        _fileSystem = fileSystem ?? new FileSystemWrapper();

        _reporter = reporter ?? Reporter.Output;
        _errorReporter = reporter ?? Reporter.Error;

        _configFilePath = result.GetValue(ToolRestoreCommandParser.ConfigOption);
        _sources = result.GetValue(ToolRestoreCommandParser.AddSourceOption);
        _verbosity = result.GetValue(ToolRestoreCommandParser.VerbosityOption);
        if (!result.HasOption(ToolRestoreCommandParser.VerbosityOption) && result.GetValue(ToolCommandRestorePassThroughOptions.InteractiveRestoreOption))
        {
            _verbosity = VerbosityOptions.minimal;
        }

        _restoreActionConfig = new RestoreActionConfig(DisableParallel: result.GetValue(ToolCommandRestorePassThroughOptions.DisableParallelOption),
            NoCache: result.GetValue(ToolCommandRestorePassThroughOptions.NoCacheOption) || result.GetValue(ToolCommandRestorePassThroughOptions.NoHttpCacheOption),
            IgnoreFailedSources: result.GetValue(ToolCommandRestorePassThroughOptions.IgnoreFailedSourcesOption),
            Interactive: result.GetValue(ToolCommandRestorePassThroughOptions.InteractiveRestoreOption));
    }

    public override int Execute()
    {
        FilePath? customManifestFileLocation = GetCustomManifestFileLocation();

        FilePath? configFile = null;
        if (!string.IsNullOrEmpty(_configFilePath))
        {
            configFile = new FilePath(_configFilePath);
        }

        IReadOnlyCollection<ToolManifestPackage> packagesFromManifest;
        try
        {
            packagesFromManifest = _toolManifestFinder.Find(customManifestFileLocation);
        }
        catch (ToolManifestCannotBeFoundException e)
        {
            if (CommandLoggingContext.IsVerbose)
            {
                _reporter.WriteLine(string.Join(Environment.NewLine, e.VerboseMessage).Yellow());
            }

            _reporter.WriteLine(e.Message.Yellow());
            _reporter.WriteLine(CliCommandStrings.NoToolsWereRestored.Yellow());
            return 0;
        }

        var toolPackageRestorer = new ToolPackageRestorer(
            _toolPackageDownloader,
            _sources,
            overrideSources: [],
            _verbosity,
            _restoreActionConfig,
            _localToolsResolverCache,
            _fileSystem);

        ToolRestoreResult[] toolRestoreResults =
            [.. packagesFromManifest
                .AsEnumerable()
                .Select(package => toolPackageRestorer.InstallPackage(package, configFile))];

        Dictionary<RestoredCommandIdentifier, ToolCommand> downloaded =
            toolRestoreResults.Select(result => result.SaveToCache)
                .Where(item => item is not null)
                .ToDictionary(pair => pair.Value.restoredCommandIdentifier, pair => pair.Value.toolCommand);

        EnsureNoCommandNameCollision(downloaded);

        _localToolsResolverCache.Save(downloaded);

        return PrintConclusionAndReturn(toolRestoreResults);
    }

    private int PrintConclusionAndReturn(ToolRestoreResult[] toolRestoreResults)
    {
        if (toolRestoreResults.Any(r => !r.IsSuccess))
        {
            _reporter.WriteLine();
            _errorReporter.WriteLine(string.Join(
                Environment.NewLine,
                toolRestoreResults.Where(r => !r.IsSuccess).Select(r => r.Message)).Red());

            var successMessage = toolRestoreResults.Where(r => r.IsSuccess).Select(r => r.Message);
            if (successMessage.Any())
            {
                _reporter.WriteLine();
                _reporter.WriteLine(string.Join(Environment.NewLine, successMessage));

            }

            _errorReporter.WriteLine(Environment.NewLine +
                                     (toolRestoreResults.Any(r => r.IsSuccess)
                                         ? CliCommandStrings.RestorePartiallyFailed
                                         : CliCommandStrings.RestoreFailed).Red());

            return 1;
        }
        else
        {
            _reporter.WriteLine(string.Join(Environment.NewLine,
                toolRestoreResults.Where(r => r.IsSuccess).Select(r => r.Message)));
            _reporter.WriteLine();
            _reporter.WriteLine(CliCommandStrings.LocalToolsRestoreWasSuccessful.Green());

            return 0;
        }
    }

    private FilePath? GetCustomManifestFileLocation()
    {
        string customFile = _parseResult.GetValue(ToolRestoreCommandParser.ToolManifestOption);
        FilePath? customManifestFileLocation;
        if (!string.IsNullOrEmpty(customFile))
        {
            customManifestFileLocation = new FilePath(customFile);
        }
        else
        {
            customManifestFileLocation = null;
        }

        return customManifestFileLocation;
    }

    private static void EnsureNoCommandNameCollision(Dictionary<RestoredCommandIdentifier, ToolCommand> dictionary)
    {
        string[] errors = [.. dictionary
            .Select(pair => (pair.Key.PackageId, pair.Key.CommandName))
            .GroupBy(packageIdAndCommandName => packageIdAndCommandName.CommandName)
            .Where(grouped => grouped.Count() > 1)
            .Select(nonUniquePackageIdAndCommandNames =>
                string.Format(CliCommandStrings.PackagesCommandNameCollisionConclusion,
                    string.Join(Environment.NewLine,
                        nonUniquePackageIdAndCommandNames.Select(
                            p => "\t" + string.Format(
                                CliCommandStrings.PackagesCommandNameCollisionForOnePackage,
                                p.CommandName.Value,
                                p.PackageId.ToString())))))];

        if (errors.Any())
        {
            throw new ToolPackageException(string.Join(Environment.NewLine, errors));
        }
    }

    public struct ToolRestoreResult
    {
        public (RestoredCommandIdentifier restoredCommandIdentifier, ToolCommand toolCommand)? SaveToCache { get; }
        public bool IsSuccess { get; }
        public string Message { get; }

        private ToolRestoreResult(
            (RestoredCommandIdentifier, ToolCommand)? saveToCache,
            bool isSuccess, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("message", nameof(message));
            }

            SaveToCache = saveToCache;
            IsSuccess = isSuccess;
            Message = message;
        }

        public static ToolRestoreResult Success(
            (RestoredCommandIdentifier, ToolCommand)? saveToCache,
            string message)
        {
            return new ToolRestoreResult(saveToCache, true, message);
        }

        public static ToolRestoreResult Failure(string message)
        {
            return new ToolRestoreResult(null, false, message);
        }

        public static ToolRestoreResult Failure(
            PackageId packageId,
            ToolPackageException toolPackageException)
        {
            return new ToolRestoreResult(null, false,
                string.Format(CliCommandStrings.PackageFailedToRestore,
                    packageId.ToString(), toolPackageException.ToString()));
        }
    }
}
