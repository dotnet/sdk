﻿// Licensed to the .NET Foundation under one or more agreements.
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
            if (parseResult.UsingRunCommandShorthandProjectOption())
            {
                Reporter.Output.WriteLine(LocalizableStrings.RunCommandProjectAbbreviationDeprecated.Yellow());
                parseResult = ModifyParseResultForShorthandProjectOption(parseResult);
            }

            var command = new RunCommand(
                noBuild: parseResult.HasOption(RunCommandParser.NoBuildOption),
                projectFileOrDirectory: parseResult.GetValue(RunCommandParser.ProjectOption),
                launchProfile: parseResult.GetValue(RunCommandParser.LaunchProfileOption),
                noLaunchProfile: parseResult.HasOption(RunCommandParser.NoLaunchProfileOption),
                noRestore: parseResult.HasOption(RunCommandParser.NoRestoreOption) || parseResult.HasOption(RunCommandParser.NoBuildOption),
                interactive: parseResult.HasOption(RunCommandParser.InteractiveOption),
                verbosity: parseResult.HasOption(CommonOptions.VerbosityOption) ? parseResult.GetValue(CommonOptions.VerbosityOption) : null,
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

        public static ParseResult ModifyParseResultForShorthandProjectOption(ParseResult parseResult)
        {
            // we know the project is going to be one of the following forms:
            //   -p:project
            //   -p project
            // so try to find those and filter them out of the arguments array
            var possibleProject = parseResult.GetRunCommandShorthandProjectValues().FirstOrDefault()!;
            var tokensMinusProject = new List<string>();
            var nextTokenMayBeProject = false;
            foreach (var token in parseResult.Tokens)
            {
                if (token.Value == "-p")
                {
                    // skip this token, if the next token _is_ the project then we'll skip that too
                    // if the next token _isn't_ the project then we'll backfill
                    nextTokenMayBeProject = true;
                    continue;
                }
                else if (token.Value == possibleProject && nextTokenMayBeProject)
                {
                    // skip, we've successfully stripped this option and value entirely
                    nextTokenMayBeProject = false;
                    continue;
                }
                else if (token.Value.StartsWith("-p") && token.Value.EndsWith(possibleProject))
                {
                    // both option and value in the same token, skip and carry on
                }
                else
                {
                    if (nextTokenMayBeProject)
                    {
                        //we skipped a -p, so backfill it
                        tokensMinusProject.Add("-p");
                    }
                    nextTokenMayBeProject = false;
                }

                tokensMinusProject.Add(token.Value);
            }

            tokensMinusProject.Add("--project");
            tokensMinusProject.Add(possibleProject);

            var tokensToParse = tokensMinusProject.ToArray();
            var newParseResult = Parser.Instance.Parse(tokensToParse);
            return newParseResult;
        }
    }
}
