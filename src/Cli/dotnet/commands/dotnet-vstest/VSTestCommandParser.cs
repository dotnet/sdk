// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.VSTest;

namespace Microsoft.DotNet.Cli
{
    internal static class VSTestCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-vstest";

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            DocumentedCommand command = new("vstest", DocsLink)
            {
                TreatUnmatchedTokensAsErrors = false
            };

            command.Options.Add(CommonOptions.TestPlatformOption);
            command.Options.Add(CommonOptions.TestFrameworkOption);
            command.Options.Add(CommonOptions.TestLoggerOption);

            command.SetAction(VSTestCommand.Run);

            return command;
        }
    }
}
