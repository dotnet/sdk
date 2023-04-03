// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;

namespace Microsoft.DotNet.Cli
{
    internal static class InternalReportinstallsuccessCommandParser
    {
        public static readonly CliArgument<string> Argument = new("internal-reportinstallsuccess-arg");

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("internal-reportinstallsuccess")
            {
                Hidden = true
            };

            command.Arguments.Add(Argument);

            command.SetAction(InternalReportinstallsuccess.Run);

            return command;
        }
    }
}
