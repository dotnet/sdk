// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Tool.Run;
using Microsoft.DotNet.Tools.Tool.Install;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Run.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolRunCommandParser
    {
        public static readonly Argument<string> CommandNameArgument = new("commandName")
        {
            HelpName = LocalizableStrings.CommandNameArgumentName,
            Description = LocalizableStrings.CommandNameArgumentDescription
        };

        public static readonly Argument<IEnumerable<string>> CommandArgument = new("toolArguments")
        {
            Description = "arguments forwarded to the tool"
        };

        public static readonly Option<bool> RollForwardOption = new("--allow-roll-forward")
        {
            Description = Tools.Tool.Install.LocalizableStrings.RollForwardOptionDescription
        };
       
        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            Command command = new("run", LocalizableStrings.CommandDescription);

            command.Arguments.Add(CommandNameArgument);
            command.Arguments.Add(CommandArgument);
            command.Options.Add(RollForwardOption);

            command.SetAction((parseResult) => new ToolRunCommand(parseResult).Execute());

            return command;
        }
    }
}
