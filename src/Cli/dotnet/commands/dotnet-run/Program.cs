// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tools.Run
{
    public partial class RunCommand
    {
        public static RunCommand FromArgs(string[] args)
        {
            var parseResult = Parser.Instance.ParseFrom("dotnet run", args);
            return FromParseResult(parseResult);
        }

        public static RunCommand FromParseResult(ParseResult parseResult)
        {
            var project = parseResult.GetValue(RunCommandParser.ProjectOption);
            if (parseResult.UsingRunCommandShorthandProjectOption())
            {
                Reporter.Output.WriteLine(LocalizableStrings.RunCommandProjectAbbreviationDeprecated.Yellow());
                var possibleProject = parseResult.GetRunCommandShorthandProjectValues().FirstOrDefault();
                if (Directory.Exists(possibleProject))
                {
                    project = RunCommandParser.FindSingleProjectInDirectory(possibleProject);
                }
                else
                {
                    project = possibleProject;
                }
            }

            var command = new RunCommand(
                noBuild: parseResult.HasOption(RunCommandParser.NoBuildOption),
                projectFileFullPath: project,
                launchProfile: parseResult.GetValue(RunCommandParser.LaunchProfileOption),
                noLaunchProfile: parseResult.HasOption(RunCommandParser.NoLaunchProfileOption),
                noRestore: parseResult.HasOption(RunCommandParser.NoRestoreOption) || parseResult.HasOption(RunCommandParser.NoBuildOption),
                interactive: parseResult.HasOption(RunCommandParser.InteractiveOption),
                restoreArgs: parseResult.OptionValuesToBeForwarded(RunCommandParser.GetCommand()).ToArray(),
                args: parseResult.GetValue(RunCommandParser.ApplicationArguments)
            );

            return command;
        }

        public static int Run(ParseResult parseResult)
        {
            parseResult.HandleDebugSwitch();

            return FromParseResult(parseResult).Execute();
        }
    }
}
