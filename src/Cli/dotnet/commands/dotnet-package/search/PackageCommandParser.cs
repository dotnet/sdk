// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.NuGet;
using LocalizableStrings = Microsoft.DotNet.Tools.Package.Search.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class PackageCommandParser
    {
        public static readonly CliArgument<string> SearchTermArgument = new("SearchTerm")
        {
            HelpName = LocalizableStrings.SearchTermArgumentName,
            Description = LocalizableStrings.SearchTermDescription
        };

        public static readonly CliOption<List<string>> Sources = new("--source")
        {
            Description = LocalizableStrings.SourceDescription,
            HelpName = LocalizableStrings.SourceArgumentName
        };

        public static readonly CliOption<int?> Take = new("--take")
        {
            Description = LocalizableStrings.TakeDescription,
            HelpName = LocalizableStrings.TakeArgumentName
        };

        public static readonly CliOption<int?> Skip = new("--skip")
        {
            Description = LocalizableStrings.SkipDescription,
            HelpName = LocalizableStrings.SkipArgumentName
        };

        public static readonly CliOption<bool> ExactMatch = new("--exact-match")
        {
            Description = LocalizableStrings.ExactMatchDescription
        };

        public static readonly CliOption<bool> Interactive = new("--interactive")
        {
            Description = LocalizableStrings.InteractiveDescription
        };

        public static readonly CliOption<bool> Prerelease = new("--prerelease")
        {
            Description = LocalizableStrings.PrereleaseDescription
        };
        public static readonly string DocsLink = "";

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new DocumentedCommand("package", DocsLink);

            command.Subcommands.Add(SearchCommand());

            return command;
        }

        private static CliCommand SearchCommand()
        {
            CliCommand searchCommand = new("search", LocalizableStrings.CommandDescription)
            {
                // The actions are not defined here and just forwarded to NuGet app
                TreatUnmatchedTokensAsErrors = false
            };

            searchCommand.Arguments.Add(SearchTermArgument);
            searchCommand.Options.Add(Sources);
            searchCommand.Options.Add(Take);
            searchCommand.Options.Add(Skip);
            searchCommand.Options.Add(ExactMatch);
            searchCommand.Options.Add(Interactive);
            searchCommand.Options.Add(Prerelease);


            searchCommand.SetAction(NuGetCommand.Run);

            return searchCommand;
        }
    }
}
