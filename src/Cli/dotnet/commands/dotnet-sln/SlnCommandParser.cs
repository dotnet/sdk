// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using NuGet.Packaging;
using LocalizableStrings = Microsoft.DotNet.Tools.Sln.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class SlnCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-sln";

        public static readonly string CommandName = "solution";
        public static readonly string CommandAlias = "sln";
        public static readonly CliArgument<string> SlnArgument = new CliArgument<string>(LocalizableStrings.SolutionArgumentName)
        {
            HelpName = LocalizableStrings.SolutionArgumentName,
            Description = LocalizableStrings.SolutionArgumentDescription,
            Arity = ArgumentArity.ZeroOrOne
        }.DefaultToCurrentDirectory();

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            DocumentedCommand command = new(CommandName, DocsLink, LocalizableStrings.AppFullName);

            command.Aliases.Add(CommandAlias);

            command.Arguments.Add(SlnArgument);
            command.Subcommands.Add(SlnAddParser.GetCommand());
            command.Subcommands.Add(SlnListParser.GetCommand());
            command.Subcommands.Add(SlnRemoveParser.GetCommand());
            command.Subcommands.Add(SlnMigrateCommandParser.GetCommand());

            command.SetAction((parseResult) => parseResult.HandleMissingCommand());

            return command;
        }

        internal static string GetSlnFileFullPath(string slnFileOrDirectory)
        {
            if (File.Exists(slnFileOrDirectory))
            {
                return slnFileOrDirectory;
            }
            if (Directory.Exists(slnFileOrDirectory))
            {
                var files = Directory.GetFiles(slnFileOrDirectory, "*.sln", SearchOption.TopDirectoryOnly);
                if (files.Length == 0)
                {
                    throw new GracefulException(CommonLocalizableStrings.CouldNotFindSolutionIn, slnFileOrDirectory);
                }
                if (files.Length > 1)
                {
                    throw new GracefulException(CommonLocalizableStrings.MoreThanOneSolutionInDirectory, slnFileOrDirectory);
                }
                return files.Single().ToString();
            }
            throw new GracefulException(CommonLocalizableStrings.CouldNotFindSolutionOrDirectory, slnFileOrDirectory);
        }
    }
}
