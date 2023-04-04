// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using Microsoft.DotNet.Tools.Sdk.Check;
using LocalizableStrings = Microsoft.DotNet.Tools.Sdk.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class SdkCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-sdk";

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            DocumentedCommand command = new("sdk", DocsLink, LocalizableStrings.AppFullName);
            command.Subcommands.Add(SdkCheckCommandParser.GetCommand());

            command.SetAction((parseResult) => parseResult.HandleMissingCommand());

            return command;
        }
    }
}
