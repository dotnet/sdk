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
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Versioning;


namespace Microsoft.DotNet.Cli.Commands.Tool.Execute
{
    internal class ToolExecuteCommand(ParseResult result, ToolManifestFinder? toolManifestFinder = null, string? currentWorkingDirectory = null) : CommandBase(result)
    {
        private readonly PackageIdentity _packageToolIdentityArgument = result.GetRequiredValue(ToolExecuteCommandParser.PackageIdentityArgument);
        private readonly IEnumerable<string> _forwardArguments = result.GetValue(ToolExecuteCommandParser.CommandArgument) ?? Enumerable.Empty<string>();
        private readonly bool _allowRollForward = result.GetValue(ToolExecuteCommandParser.RollForwardOption);
        private readonly string? _configFile = result.GetValue(ToolExecuteCommandParser.ConfigOption);
        private readonly string[] _sources = result.GetValue(ToolExecuteCommandParser.SourceOption) ?? [];
        private readonly string[] _addSource = result.GetValue(ToolExecuteCommandParser.AddSourceOption) ?? [];
        private readonly bool _ignoreFailedSources = result.GetValue(ToolCommandRestorePassThroughOptions.IgnoreFailedSourcesOption);
        private readonly bool _interactive = result.GetValue(ToolExecuteCommandParser.InteractiveOption);
        private readonly VerbosityOptions _verbosity = result.GetValue(ToolExecuteCommandParser.VerbosityOption);
        private readonly bool _yes = result.GetValue(ToolExecuteCommandParser.YesOption);

        //  TODO: Does result.OptionValuesToBeForwarded work here?
        private readonly IToolPackageDownloader _toolPackageDownloader = ToolPackageFactory.CreateToolPackageStoresAndDownloader(
                additionalRestoreArguments: result.OptionValuesToBeForwarded(ToolExecuteCommandParser.GetCommand())).Item3;

        //  TODO: Make sure to add these options to the command
        private readonly RestoreActionConfig _restoreActionConfig = new RestoreActionConfig(DisableParallel: result.GetValue(ToolCommandRestorePassThroughOptions.DisableParallelOption),
            NoCache: result.GetValue(ToolCommandRestorePassThroughOptions.NoCacheOption) || result.GetValue(ToolCommandRestorePassThroughOptions.NoHttpCacheOption),
            IgnoreFailedSources: result.GetValue(ToolCommandRestorePassThroughOptions.IgnoreFailedSourcesOption),
            Interactive: result.GetValue(ToolCommandRestorePassThroughOptions.InteractiveRestoreOption));

        //  TODO: Use prerelease
        private readonly bool _prerelease = result.GetValue(ToolExecuteCommandParser.PrereleaseOption);

        private readonly ToolManifestFinder _toolManifestFinder = toolManifestFinder ?? new ToolManifestFinder(new DirectoryPath(currentWorkingDirectory ?? Directory.GetCurrentDirectory()));

        public override int Execute()
        {
            VersionRange versionRange = _parseResult.GetVersionRange();
            PackageId packageId = new PackageId(_packageToolIdentityArgument.Id);

            //  Look in local tools manifest first, but only if version is not specified
            if (versionRange == null)
            {
                var localToolsResolverCache = new LocalToolsResolverCache();

                if (_toolManifestFinder.TryFindPackageId(packageId, out var toolManifestPackage))
                {
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


            if (!UserAgreedToRunFromSource())
            {
                throw new GracefulException(CliCommandStrings.ToolRunFromSourceUserConfirmationFailed, isUserError: true);
            }

            if (_allowRollForward)
            {
                _forwardArguments.Append("--allow-roll-forward");
            }


            string tempDirectory = NuGetEnvironment.GetFolderPath(NuGetFolderPath.Temp);

            IToolPackage toolPackage = _toolPackageDownloader.InstallPackage(
                new PackageLocation(
                    nugetConfig: _configFile != null ? new(_configFile) : null,
                    sourceFeedOverrides: _sources,
                    additionalFeeds: _addSource),
                packageId: packageId,
                verbosity: _verbosity,
                versionRange: versionRange,
                isGlobalToolRollForward: _allowRollForward, // Needed to update .runtimeconfig.json
                restoreActionConfig: new(
                    IgnoreFailedSources: _ignoreFailedSources,
                    Interactive: _interactive));

            CommandSpec commandSpec = MuxerCommandSpecMaker.CreatePackageCommandSpecUsingMuxer(toolPackage.Command.Executable.ToString(), _forwardArguments);
            var command = CommandFactoryUsingResolver.Create(commandSpec);
            var result = command.Execute();
            return result.ExitCode;
        }

        private bool UserAgreedToRunFromSource()
        {
            if (_yes)
            {
                return true;
            }

            if (!_interactive)
            {
                return false;
            }

            // TODO: Use a better way to ask for user input
            Console.Write(CliCommandStrings.ToolRunFromSourceUserConfirmationPrompt);
            bool userAccepted = Console.ReadKey().Key == ConsoleKey.Y;

            if (_verbosity >= VerbosityOptions.detailed)
            {
                Console.WriteLine();
                Console.WriteLine(new String('-', CliCommandStrings.ToolRunFromSourceUserConfirmationPrompt.Length));
            }
            else
            {
                // Clear the line
                Console.Write("\r" + new string(' ', CliCommandStrings.ToolRunFromSourceUserConfirmationPrompt.Length + 1) + "\r");
            }

            return userAccepted;
        }
    }
}
