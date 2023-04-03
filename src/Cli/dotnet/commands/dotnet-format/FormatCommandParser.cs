// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Tools.Format
{
    internal static partial class FormatCommandParser
    {
        public static readonly CliArgument<string[]> Arguments = new("arguments");

        public static readonly string DocsLink = "https://aka.ms/dotnet-format";

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            var formatCommand = new DocumentedCommand("format", DocsLink)
            {
                Arguments
            };
            formatCommand.SetAction((ParseResult parseResult) => FormatCommand.Run(parseResult.GetValue(Arguments)));
            return formatCommand;
        }
    }
}
