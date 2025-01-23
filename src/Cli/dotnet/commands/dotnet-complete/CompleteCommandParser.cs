// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli
{
    internal static class CompleteCommandParser
    {
        public static readonly Argument<string> PathArgument = new("path");

        public static readonly Option<int?> PositionOption = new("--position")
        {
            HelpName = "command"
        };

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            Command command = new("complete")
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
