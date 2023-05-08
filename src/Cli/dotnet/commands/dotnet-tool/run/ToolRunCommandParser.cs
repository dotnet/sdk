// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Tool.Run;
using System.Collections.Generic;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Run.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolRunCommandParser
    {
        public static readonly Argument<string> CommandNameArgument = new Argument<string>(LocalizableStrings.CommandNameArgumentName)
        {
            Description = LocalizableStrings.CommandNameArgumentDescription
        };

        public static readonly Argument<IEnumerable<string>> CommandArgument = new Argument<IEnumerable<string>>("toolArguments", "arguments forwarded to the tool");
       
        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new Command("run", LocalizableStrings.CommandDescription);

            command.AddArgument(CommandNameArgument);
            command.AddArgument(CommandArgument);

            command.SetHandler((parseResult) => new ToolRunCommand(parseResult).Execute());

            return command;
        }
    }
}
