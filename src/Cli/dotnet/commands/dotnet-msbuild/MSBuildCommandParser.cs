// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using LocalizableStrings = Microsoft.DotNet.Tools.Build.LocalizableStrings;

namespace Microsoft.DotNet.Tools.MSBuild
{
    internal static class MSBuildCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-msbuild";

        public static readonly CliArgument<string[]> Arguments = new("arguments");

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            var command = new DocumentedCommand("msbuild", DocsLink, LocalizableStrings.AppFullName)
            {
                Arguments
            };

            command.SetAction((ParseResult parseResult) => MSBuildCommand.Run(parseResult.GetValue(Arguments)));

            return command;
        }
    }
}
