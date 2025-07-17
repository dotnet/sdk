// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandFactory;
using Microsoft.DotNet.Cli.CommandFactory.CommandResolution;
using Microsoft.DotNet.Cli.Commands.Tool.Install;
using Microsoft.DotNet.Cli.Commands.Tool.Restore;
using Microsoft.DotNet.Cli.Commands.Tool.Run;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.ToolManifest;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Configuration;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.Commands.Tool.Execute;

internal class ToolExecuteCommand(ParseResult result, ToolManifestFinder? toolManifestFinder = null, string? currentWorkingDirectory = null) : CommandBase(result)
{
    const int ERROR_CANCELLED = 1223; //  Windows error code for "Operation canceled by user"

    private readonly PackageIdentityWithRange _packageToolIdentityArgument = result.GetValue(ToolExecuteCommandParser.PackageIdentityArgument);
    private readonly IEnumerable<string> _forwardArguments = result.GetValue(ToolExecuteCommandParser.CommandArgument) ?? Enumerable.Empty<string>();
    private readonly bool _allowRollForward = result.GetValue(ToolExecuteCommandParser.RollForwardOption);
    private readonly string? _configFile = result.GetValue(ToolExecuteCommandParser.ConfigOption);
    private readonly string[] _sources = result.GetValue(ToolExecuteCommandParser.SourceOption) ?? [];
    private readonly string[] _addSource = result.GetValue(ToolExecuteCommandParser.AddSourceOption) ?? [];
    private readonly bool _interactive = result.GetValue(ToolExecuteCommandParser.InteractiveOption);
    private readonly VerbosityOptions _verbosity = result.GetValue(ToolExecuteCommandParser.VerbosityOption);
    private readonly IToolPackageDownloader _toolPackageDownloader = ToolPackageFactory.CreateToolPackageStoresAndDownloader().downloader;

    private readonly RestoreActionConfig _restoreActionConfig = new RestoreActionConfig(DisableParallel: result.GetValue(ToolCommandRestorePassThroughOptions.DisableParallelOption),
        NoCache: result.GetValue(ToolCommandRestorePassThroughOptions.NoCacheOption) || result.GetValue(ToolCommandRestorePassThroughOptions.NoHttpCacheOption),
        IgnoreFailedSources: result.GetValue(ToolCommandRestorePassThroughOptions.IgnoreFailedSourcesOption),
        Interactive: result.GetValue(ToolExecuteCommandParser.InteractiveOption));

    private readonly ToolManifestFinder _toolManifestFinder = toolManifestFinder ?? new ToolManifestFinder(new DirectoryPath(currentWorkingDirectory ?? Directory.GetCurrentDirectory()));

    public override int Execute()
    {
        VersionRange? versionRange = _parseResult.GetVersionRange();
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

                var restoreResult = toolPackageRestorer.InstallPackage(toolManifestPackage, _configFile == null ? null : new FilePath(_configFile));

                if (!restoreResult.IsSuccess)
                {
                    Reporter.Error.WriteLine(restoreResult.Message.Red());
                    return 1;
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

        (var bestVersion, var packageSource) = _toolPackageDownloader.GetNuGetVersion(packageLocation, packageId, _verbosity, versionRange, _restoreActionConfig);
        toolLocationActivity?.SetTag("tool.exec.kind", "one-shot");
        toolLocationActivity?.Stop();

        //  TargetFramework is null, which means to use the current framework.  Global tools can override the target framework to use (or select assets for),
        //  but we don't support this for local or one-shot tools.
        if (!_toolPackageDownloader.TryGetDownloadedTool(packageId, bestVersion, targetFramework: null, verbosity: _verbosity, out var toolPackage))
        {
            if (!UserAgreedToRunFromSource(packageId, bestVersion, packageSource))
            {
                if (_interactive)
                {
                    Reporter.Error.WriteLine(CliCommandStrings.ToolDownloadCanceled.Red().Bold());
                    return ERROR_CANCELLED;
                }
                else
                {
                    Reporter.Error.WriteLine(CliCommandStrings.ToolDownloadNeedsConfirmation.Red().Bold());
                    return 1;
                }
            }

            //  We've already determined which source we will use and displayed that in a confirmation message to the user.
            //  So set the package location here to override the source feeds to just the source we already resolved to.
            //  This does mean that we won't work with feeds that have a primary package but where the RID-specific packages are on
            //  other feeds, but this is probably OK.
            var downloadPackageLocation = new PackageLocation(
                nugetConfig: _configFile != null ? new(_configFile) : null,
                sourceFeedOverrides: [packageSource.Source],
                additionalFeeds: _addSource);

            toolPackage = _toolPackageDownloader.InstallPackage(
                downloadPackageLocation,
                packageId: packageId,
                verbosity: _verbosity,
                versionRange: new VersionRange(bestVersion, true, bestVersion, true),
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

    private bool UserAgreedToRunFromSource(PackageId packageId, NuGetVersion version, PackageSource source)
    {
        string promptMessage = string.Format(CliCommandStrings.ToolDownloadConfirmationPrompt, packageId, version.ToString(), source.Source);
        return InteractiveConsole.Confirm(promptMessage, _parseResult, acceptEscapeForFalse: true) == true;
    }
}
