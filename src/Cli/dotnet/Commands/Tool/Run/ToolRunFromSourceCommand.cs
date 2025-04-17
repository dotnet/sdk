// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandFactory;
using Microsoft.DotNet.Cli.CommandFactory.CommandResolution;
using Microsoft.DotNet.Cli.Commands.Tool.Install;
using Microsoft.DotNet.Cli.ToolManifest;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.Commands.Tool.Run
{
    internal class ToolRunFromSourceCommand(
        ParseResult result) : CommandBase(result)
    {
        private readonly string _toolCommandName = result.GetValue(ToolRunCommandParser.CommandNameArgument);
        private readonly IEnumerable<string> _forwardArguments = result.GetValue(ToolRunCommandParser.CommandArgument);
        private readonly bool _allowRollForward = result.GetValue(ToolRunCommandParser.RollForwardOption);
        private readonly string _configFile = result.GetValue(ToolRunCommandParser.FromSourceConfigFile);
        private readonly string[] _sources = result.GetValue(ToolRunCommandParser.FromSourceSourceOption) ?? [];
        private readonly string[] _addSource = result.GetValue(ToolRunCommandParser.FromSourceAddSourceOption) ?? [];
        private readonly bool _ignoreFailedSources = result.GetValue(ToolCommandRestorePassThroughOptions.IgnoreFailedSourcesOption);
        private readonly bool _interactive = result.GetValue(ToolCommandRestorePassThroughOptions.InteractiveOption);
        private readonly VerbosityOptions _verbosity = result.GetValue(ToolRunCommandParser.VerbosityOption) ?? VerbosityOptions.normal;

        public override int Execute()
        {
            if (!UserAgreedToExecuteFromSource())
            {
                return 1;
            }

            PackageId packageId = new(_toolCommandName);
            VersionRange versionRange = _parseResult.GetVersionRange();

            string tempDirectory = PathUtilities.CreateTempSubdirectory();

            IToolManifestFinder toolManifestFinder = new ToolManifestFinder(new(tempDirectory));
            IToolManifestEditor toolManifestEditor = new ToolManifestEditor();
            FilePath toolManifestPath = toolManifestFinder.FindFirst(true);

            ToolPackageStoreAndQuery toolPackageStoreAndQuery = new(new(tempDirectory));
            ToolPackageDownloader toolPackageDownloader = new(toolPackageStoreAndQuery);

            IToolPackage toolPackage = toolPackageDownloader.InstallPackage(
                new PackageLocation(
                    nugetConfig: _configFile != null ? new FilePath(_configFile) : null,
                    rootConfigDirectory: toolManifestPath.GetDirectoryPath().GetParentPath(),
                    sourceFeedOverrides: _sources,
                    additionalFeeds: _addSource),
                packageId: packageId,
                verbosity: _verbosity,
                versionRange: versionRange,
                restoreActionConfig: new(
                    IgnoreFailedSources: _ignoreFailedSources,
                    Interactive: _interactive));

            LocalToolsResolverCache localToolsResolverCache = new();
            localToolsResolverCache.SaveToolPackage(toolPackage, BundledTargetFramework.GetTargetFrameworkMoniker());

            toolManifestEditor.Add(
                toolManifestPath,
                packageId,
                toolPackage.Version,
                [toolPackage.Command.Name],
                rollForward: _allowRollForward);

            LocalToolsCommandResolver localToolsCommandResolver = new();

            CommandSpec commandSpec = localToolsCommandResolver.ResolveStrict(new()
            {
                CommandName = $"dotnet-{toolPackage.Command.Name}",
                CommandArguments = _forwardArguments,
            }, _allowRollForward);

            if (commandSpec == null)
            {
                throw new GracefulException(
                    string.Format(CliCommandStrings.CannotFindCommandName, _toolCommandName),
                    isUserError: false);
            }

            var command = CommandFactoryUsingResolver.Create(commandSpec);
            var result = command.Execute();

            return result.ExitCode;
        }
        private bool UserAgreedToExecuteFromSource()
        {
            // TODO: Use a better way to ask for user input
            Console.WriteLine("Tool will be run from source. Accept? [yn]");
            return Console.ReadKey().Key == ConsoleKey.Y;
        }
    }
}
