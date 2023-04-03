// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;

namespace Microsoft.DotNet.Cli
{
    internal static class CompleteCommandParser
    {
        public static readonly CliArgument<string> PathArgument = new("path");

        public static readonly CliOption<int?> PositionOption = new("--position")
        {
            HelpName = "command"
        };

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("complete")
            {
                Hidden = true
            };

            command.Arguments.Add(PathArgument);
            command.Options.Add(PositionOption);

            command.SetAction(CompleteCommand.Run);

            return command;
        }
    }
}
