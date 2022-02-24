// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Tools.VSTest;

namespace Microsoft.DotNet.Cli
{
    internal static class VSTestCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-vstest";

        public static readonly Argument<string[]> ForwardedArguments = new Argument<string[]>("forwardedArguments", "Arguments to forward to VSTest");

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new DocumentedCommand("vstest", DocsLink);

            command.AddOption(CommonOptions.TestPlatformOption);
            command.AddOption(CommonOptions.TestFrameworkOption);
            command.AddOption(CommonOptions.TestLoggerOption);
            command.AddArgument(ForwardedArguments);
            command.SetHandler(VSTestCommand.Run);

            return command;
        }
    }
}
