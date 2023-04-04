// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using Microsoft.DotNet.Tools.Tool.Run;
using System.Collections.Generic;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Run.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolRunCommandParser
    {
        public static readonly CliArgument<string> CommandNameArgument = new("commandName")
        {
            HelpName = LocalizableStrings.CommandNameArgumentName,
            Description = LocalizableStrings.CommandNameArgumentDescription
        };

        public static readonly CliArgument<IEnumerable<string>> CommandArgument = new("toolArguments")
        {
            Description = "arguments forwarded to the tool"
        };
       
        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("run", LocalizableStrings.CommandDescription);

            command.Arguments.Add(CommandNameArgument);
            command.Arguments.Add(CommandArgument);

            command.SetAction((parseResult) => new ToolRunCommand(parseResult).Execute());

            return command;
        }
    }
}
