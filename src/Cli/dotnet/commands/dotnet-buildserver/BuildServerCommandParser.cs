// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.BuildServer.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class BuildServerCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-build-server";

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new DocumentedCommand("build-server", DocsLink, LocalizableStrings.CommandDescription);

            command.Subcommands.Add(ServerShutdownCommandParser.GetCommand());

            command.SetAction((parseResult) => parseResult.HandleMissingCommand());

            return command;
        }
    }
}
