// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli
{
    internal static class InternalReportinstallsuccessCommandParser
    {
        public static readonly Argument<string> Argument = new("internal-reportinstallsuccess-arg");

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            Command command = new("internal-reportinstallsuccess")
            {
                Hidden = true
            };

            command.Arguments.Add(Argument);

            command.SetAction(InternalReportinstallsuccess.Run);

            return command;
        }
    }
}
