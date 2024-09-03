// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
