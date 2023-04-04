// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using Microsoft.DotNet.Tools.VSTest;

namespace Microsoft.DotNet.Cli
{
    internal static class VSTestCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-vstest";

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            DocumentedCommand command = new("vstest", DocsLink);

            command.Options.Add(CommonOptions.TestPlatformOption);
            command.Options.Add(CommonOptions.TestFrameworkOption);
            command.Options.Add(CommonOptions.TestLoggerOption);

            command.SetAction(VSTestCommand.Run);

            return command;
        }
    }
}
