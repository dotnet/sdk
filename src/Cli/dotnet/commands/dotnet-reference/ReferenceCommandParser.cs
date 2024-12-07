// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.Remove.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ReferenceCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-reference";

        public static readonly CliArgument<string> ProjectArgument = new CliArgument<string>(CommonLocalizableStrings.ProjectArgumentName)
        {
            Description = CommonLocalizableStrings.ProjectArgumentDescription
        }.DefaultToCurrentDirectory();

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            var command = new DocumentedCommand("reference", DocsLink, LocalizableStrings.NetRemoveCommand);

            command.Arguments.Add(ProjectArgument);
            command.Subcommands.Add(ReferenceAddCommandParser.GetCommand());
            command.Subcommands.Add(ReferenceListCommandParser.GetCommand());
            command.Subcommands.Add(ReferenceRemoveCommandParser.GetCommand());

            command.SetAction((parseResult) => parseResult.HandleMissingCommand());

            return command;
        }
    }
}
