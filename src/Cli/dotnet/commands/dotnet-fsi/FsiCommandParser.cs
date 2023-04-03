// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using Microsoft.DotNet.Tools.Fsi;

namespace Microsoft.DotNet.Cli
{
    internal static class FsiCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-fsi";

        public static readonly CliArgument<string[]> Arguments = new("arguments");

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            DocumentedCommand command = new("fsi", DocsLink) { Arguments };

            command.SetAction((ParseResult parseResult) => FsiCommand.Run(parseResult.GetValue(Arguments)));

            return command;
        }
    }
}
