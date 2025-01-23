// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Tools.Help
{
    internal static class HelpCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-help";

        public static readonly Argument<string[]> Argument = new(LocalizableStrings.CommandArgumentName)
        {
            Description = LocalizableStrings.CommandArgumentDescription,
            Arity = ArgumentArity.ZeroOrMore
        };

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            DocumentedCommand command = new("help", DocsLink, LocalizableStrings.AppFullName);

            command.Arguments.Add(Argument);

            command.SetAction(HelpCommand.Run);

            return command;
        }
    }
}

