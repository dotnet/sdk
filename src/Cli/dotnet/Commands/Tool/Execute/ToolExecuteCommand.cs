// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandFactory;
using Microsoft.DotNet.Cli.CommandFactory.CommandResolution;
using Microsoft.DotNet.Cli.Commands.Tool.Install;
using Microsoft.DotNet.Cli.Commands.Tool.Restore;
using Microsoft.DotNet.Cli.Commands.Tool.Run;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.ToolManifest;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.Commands.Tool.Execute;

internal sealed class ToolExecuteCommand : CommandBase<ToolExecuteCommandDefinitionBase>
{
    internal const int DefaultFeedTimeoutMilliseconds = 100;
    internal const string FeedTimeoutEnvironmentVariableName = "DNX_FEED_TIMEOUT_MILLISECONDS";

    private readonly PackageIdentityWithRange _packageToolIdentityArgument;
    private readonly IEnumerable<string> _forwardArguments;
    private readonly bool _allowRollForward;
    private readonly string? _configFile;
    private readonly string[] _sources;
    private readonly string[] _addSource;
    private readonly VerbosityOptions _verbosity;
    private readonly IToolPackageDownloader _toolPackageDownloader =
        ToolPackageFactory.CreateToolPackageStoresAndDownloader().downloader;

    private readonly RestoreActionConfig _restoreActionConfig;

    private readonly ToolManifestFinder _toolManifestFinder;

    public ToolExecuteCommand(ParseResult result, ToolManifestFinder? toolManifestFinder = null, string? currentWorkingDirectory = null)
        : base(result)
    {
        _packageToolIdentityArgument = result.GetValue(Definition.PackageIdentityArgument);
        _forwardArguments = result.GetValue(Definition.CommandArgument) ?? [];
        _allowRollForward = result.GetValue(Definition.RollForwardOption);
        _configFile = result.GetValue(Definition.ConfigOption);
        _sources = result.GetValue(Definition.SourceOption) ?? [];
        _addSource = result.GetValue(Definition.AddSourceOption) ?? [];
        _verbosity = result.GetValue(Definition.VerbosityOption);
        _restoreActionConfig = Definition.RestoreOptions.ToRestoreActionConfig(result);
        _toolManifestFinder = toolManifestFinder ?? new ToolManifestFinder(new DirectoryPath(currentWorkingDirectory ?? Directory.GetCurrentDirectory()));
    }

    public override int Execute() => ExecuteAsync(CancellationToken.None).GetAwaiter().GetResult();

    internal async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        var versionRange = VersionRangeUtilities.GetVersionRange(
            _packageToolIdentityArgument.VersionRange?.OriginalString,
            _parseResult.GetValue(Definition.VersionOption),
            _parseResult.GetValue(Definition.PrereleaseOption));

        PackageId packageId = new PackageId(_packageToolIdentityArgument.Id);

        var toolLocationActivity = Activities.Source.StartActivity("find-tool");
        toolLocationActivity?.SetTag("tool.package.id", packageId.ToString());
        toolLocationActivity?.SetTag("tool.package.version", versionRange?.ToString() ?? "latest");

        //  Look in local tools manifest first, but only if version is not specified
        if (versionRange == null)
        {
            var localToolsResolverCache = new LocalToolsResolverCache();

            if (_toolManifestFinder.TryFindPackageId(packageId, out var toolManifestPackage))
            {
                toolLocationActivity?.SetTag("tool.exec.kind", "local");
                toolLocationActivity?.Stop();

                var toolPackageRestorer = new ToolPackageRestorer(
                    _toolPackageDownloader,
                    _sources,
                    overrideSources: [],
                    _verbosity,
                    _restoreActionConfig,
                    localToolsResolverCache,
                    new FileSystemWrapper());

                var restoreResult = await toolPackageRestorer.InstallPackageAsync(
                    toolManifestPackage,
                    _configFile == null ? null : new FilePath(_configFile),
                    cancellationToken);

                if (!restoreResult.IsSuccess)
                {
                    Reporter.Error.WriteLine(restoreResult.Message.Red());
                    return 1;
                }

                if (restoreResult.SaveToCache is not null)
                {
                    localToolsResolverCache.Save(new Dictionary<RestoredCommandIdentifier, ToolCommand>
                    {
                        { restoreResult.SaveToCache.Value.restoredCommandIdentifier, restoreResult.SaveToCache.Value.toolCommand }
                    });
                }

                var localToolsCommandResolver = new LocalToolsCommandResolver(
                    _toolManifestFinder,
                    localToolsResolverCache);

                return ToolRunCommand.ExecuteCommand(localToolsCommandResolver, toolManifestPackage.CommandNames.Single().Value, _forwardArguments, _allowRollForward);
            }
        }

        var packageLocation = new PackageLocation(
                nugetConfig: _configFile != null ? new(_configFile) : null,
                sourceFeedOverrides: _sources,
                additionalFeeds: _addSource);

        VersionRange effectiveVersionRange = versionRange ?? VersionRange.Parse("*");
        IToolPackage? toolPackage = null;
        PackageSource? packageSource = null;
        NuGetVersion? bestVersion = null;

        if (!_restoreActionConfig.NoCache &&
            _toolPackageDownloader.TryGetBestDownloadedTool(
                packageId,
                effectiveVersionRange,
                targetFramework: null,
                verbosity: _verbosity,
                out IToolPackage? cachedToolPackage))
        {
            if (IsExactVersion(effectiveVersionRange))
            {
                toolPackage = cachedToolPackage;
            }
            else
            {
                (bestVersion, packageSource) = await ProbeFeedsForBestVersionAsync(
                    _toolPackageDownloader,
                    packageLocation,
                    packageId,
                    effectiveVersionRange,
                    cachedToolPackage.Version,
                    _verbosity,
                    _restoreActionConfig,
                    cancellationToken);

                if (bestVersion is null || bestVersion <= cachedToolPackage.Version)
                {
                    toolPackage = cachedToolPackage;
                }
            }
        }

        if (toolPackage is null && bestVersion is null)
        {
            (bestVersion, packageSource) = await _toolPackageDownloader.GetNuGetVersionAsync(
                packageLocation,
                packageId,
                _verbosity,
                effectiveVersionRange,
                _restoreActionConfig,
                cancellationToken);
        }

        toolLocationActivity?.SetTag("tool.exec.kind", "one-shot");
        toolLocationActivity?.Stop();

        //  TargetFramework is null, which means to use the current framework.  Global tools can override the target framework to use (or select assets for),
        //  but we don't support this for local or one-shot tools.
        if (toolPackage is null &&
            !_toolPackageDownloader.TryGetDownloadedTool(packageId, bestVersion!, targetFramework: null, verbosity: _verbosity, out toolPackage))
        {
            //  We've already determined which source we will use and will use it to download the package.
            //  So set the package location here to override the source feeds to just the source we already resolved to.
            //  This does mean that we won't work with feeds that have a primary package but where the RID-specific packages are on
            //  other feeds, but this is probably OK.
            var downloadPackageLocation = new PackageLocation(
                nugetConfig: _configFile != null ? new(_configFile) : null,
                sourceFeedOverrides: _sources,
                additionalFeeds: _addSource,
                packageSourceOverrides: [packageSource!]);

            toolPackage = _toolPackageDownloader.InstallPackage(
                downloadPackageLocation,
                packageId: packageId,
                verbosity: _verbosity,
                versionRange: new VersionRange(bestVersion!, true, bestVersion!, true),
                isGlobalToolRollForward: false,
                restoreActionConfig: _restoreActionConfig);
        }

        using var toolExecuteActivity = Activities.Source.StartActivity("execute-tool");
        toolExecuteActivity?.SetTag("tool.package.id", packageId.ToString());
        toolExecuteActivity?.SetTag("tool.package.version", toolPackage.Version.ToString());
        toolExecuteActivity?.SetTag("tool.runner", toolPackage.Command.Runner);
        var commandSpec = ToolCommandSpecCreator.CreateToolCommandSpec(toolPackage.Command.Name.Value, toolPackage.Command.Executable.Value, toolPackage.Command.Runner, _allowRollForward, _forwardArguments);
        var command = CommandFactoryUsingResolver.Create(commandSpec);
        var result = command.Execute();
        return result.ExitCode;
    }

    internal static async Task<(NuGetVersion? version, PackageSource? source)> ProbeFeedsForBestVersionAsync(
        IToolPackageDownloader toolPackageDownloader,
        PackageLocation packageLocation,
        PackageId packageId,
        VersionRange versionRange,
        NuGetVersion cachedVersion,
        VerbosityOptions verbosity,
        RestoreActionConfig restoreActionConfig,
        CancellationToken cancellationToken)
    {
        using var timeoutTokenSource = new CancellationTokenSource(GetFeedTimeoutMilliseconds());
        using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutTokenSource.Token);

        try
        {
            return await toolPackageDownloader.GetNuGetVersionAsync(
                packageLocation,
                packageId,
                verbosity,
                versionRange,
                restoreActionConfig,
                linkedTokenSource.Token);
        }
        catch (OperationCanceledException) when (timeoutTokenSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return (cachedVersion, null);
        }
        catch (Exception e) when (IsFeedProbeFailure(e))
        {
            return (cachedVersion, null);
        }
    }

    internal static int GetFeedTimeoutMilliseconds()
        => int.TryParse(
                Environment.GetEnvironmentVariable(FeedTimeoutEnvironmentVariableName),
                out int timeoutMilliseconds) &&
            timeoutMilliseconds > 0
                ? timeoutMilliseconds
                : DefaultFeedTimeoutMilliseconds;

    private static bool IsExactVersion(VersionRange versionRange)
        => versionRange.HasLowerAndUpperBounds &&
           versionRange.MinVersion == versionRange.MaxVersion &&
           versionRange.IsMinInclusive &&
           versionRange.IsMaxInclusive;

    private static bool IsFeedProbeFailure(Exception exception)
        => exception is NuGetPackageNotFoundException or FatalProtocolException or HttpRequestException ||
           exception.InnerException is not null && IsFeedProbeFailure(exception.InnerException);
}
