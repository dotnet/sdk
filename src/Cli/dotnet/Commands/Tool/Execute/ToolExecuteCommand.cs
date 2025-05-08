// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandFactory;
using Microsoft.DotNet.Cli.CommandFactory.CommandResolution;
using Microsoft.DotNet.Cli.Commands.Tool.Install;
using Microsoft.DotNet.Cli.ToolPackage;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.Commands.Tool.Execute
{
    internal class ToolExecuteCommand(ParseResult result) : CommandBase(result)
    {
        private readonly PackageIdentity? _packageToolIdentityArgument = result.GetValue(ToolExecuteCommandParser.PackageIdentityArgument);
        private readonly IEnumerable<string> _forwardArguments = result.GetValue(ToolExecuteCommandParser.CommandArgument);
        private readonly bool _allowRollForward = result.GetValue(ToolExecuteCommandParser.RollForwardOption);
        private readonly string _configFile = result.GetValue(ToolExecuteCommandParser.ConfigOption);
        private readonly string[] _sources = result.GetValue(ToolExecuteCommandParser.SourceOption) ?? [];
        private readonly string[] _addSource = result.GetValue(ToolExecuteCommandParser.AddSourceOption) ?? [];
        private readonly bool _ignoreFailedSources = result.GetValue(ToolCommandRestorePassThroughOptions.IgnoreFailedSourcesOption);
        private readonly bool _interactive = result.GetValue(ToolExecuteCommandParser.InteractiveOption);
        private readonly VerbosityOptions _verbosity = result.GetValue(ToolExecuteCommandParser.VerbosityOption);
        private readonly bool _yes = result.GetValue(ToolExecuteCommandParser.YesOption);
        private readonly bool _prerelease = result.GetValue(ToolExecuteCommandParser.PrereleaseOption);

        public override int Execute()
        {
            if (!UserAgreedToRunFromSource() || _packageToolIdentityArgument is null)
            {
                return 1;
            }

            if (_allowRollForward)
            {
                _forwardArguments.Append("--allow-roll-forward");
            }

            PackageId packageId = new PackageId(_packageToolIdentityArgument.Id);

            VersionRange versionRange = _parseResult.GetVersionRange();

            string tempDirectory = NuGetEnvironment.GetFolderPath(NuGetFolderPath.Temp);

            ToolPackageStoreAndQuery toolPackageStoreAndQuery = new(new(tempDirectory));
            ToolPackageDownloader toolPackageDownloader = new(toolPackageStoreAndQuery);

            IToolPackage toolPackage = toolPackageDownloader.InstallPackage(
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
