using System;
using System.CommandLine;
using Microsoft.DotNet.Tools.Package.Search;
using LocalizableStrings = Microsoft.DotNet.Tools.Package.Search.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class PackageSearchCommandParser
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

        private static readonly CliCommand command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new CliCommand("search", LocalizableStrings.CommandDescription);

            command.Arguments.Add(SearchTermArgument);

            command.Options.Add(Sources);
            command.Options.Add(Take);
            command.Options.Add(Skip);
            command.Options.Add(ExactMatch);
            command.Options.Add(Interactive);
            command.Options.Add(Prerelease);

            command.SetAction((parseResult) => new PackageSearchCommand(parseResult).Execute());

            return command;
        }
    }
}
