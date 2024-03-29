// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.Package.Search.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class PackageSearchCommandParser
    {
        public static readonly CliArgument<string> SearchTermArgument = new CliArgument<string>("SearchTerm")
        {
            HelpName = LocalizableStrings.SearchTermArgumentName,
            Description = LocalizableStrings.SearchTermDescription,
            Arity = ArgumentArity.ZeroOrOne
        };

        public static readonly CliOption Sources =  new ForwardedOption<IEnumerable<string>>("--source")
        {
            Description = LocalizableStrings.SourceDescription,
            HelpName = LocalizableStrings.SourceArgumentName
        }.ForwardAsManyArgumentsEachPrefixedByOption("--source")
        .AllowSingleArgPerToken();

        public static readonly CliOption<string> Take = new ForwardedOption<string>("--take")
        {
            Description = LocalizableStrings.TakeDescription,
            HelpName = LocalizableStrings.TakeArgumentName
        }.ForwardAsSingle(o => $"--take:{o}");

        public static readonly CliOption<string> Skip = new ForwardedOption<string>("--skip")
        {
            Description = LocalizableStrings.SkipDescription,
            HelpName = LocalizableStrings.SkipArgumentName
        }.ForwardAsSingle(o => $"--skip:{o}");

        public static readonly CliOption<bool> ExactMatch = new ForwardedOption<bool>("--exact-match")
        {
            Description = LocalizableStrings.ExactMatchDescription
        }.ForwardAs("--exact-match");

        public static readonly CliOption<bool> Interactive = new ForwardedOption<bool>("--interactive")
        {
            Description = LocalizableStrings.InteractiveDescription
        }.ForwardAs("--interactive");

        public static readonly CliOption<bool> Prerelease = new ForwardedOption<bool>("--prerelease")
        {
            Description = LocalizableStrings.PrereleaseDescription
        }.ForwardAs("--prerelease");

        public static readonly CliOption<string> ConfigFile = new ForwardedOption<string>("--configfile")
        {
            Description = LocalizableStrings.ConfigFileDescription,
            HelpName = LocalizableStrings.ConfigFileArgumentName
        }.ForwardAsSingle(o => $"--configfile:{o}");

        public static readonly CliOption<string> Format = new ForwardedOption<string>("--format")
        {
            Description = LocalizableStrings.FormatDescription,
            HelpName = LocalizableStrings.FormatArgumentName
        }.ForwardAsSingle(o => $"--format:{o}");

        public static readonly CliOption<string> Verbosity = new ForwardedOption<string>("--verbosity")
        {
            Description = LocalizableStrings.VerbosityDescription,
            HelpName = LocalizableStrings.VerbosityArgumentName
        }.ForwardAsSingle(o => $"--verbosity:{o}");

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand searchCommand = new("search", LocalizableStrings.CommandDescription);

            searchCommand.Arguments.Add(SearchTermArgument);
            searchCommand.Options.Add(Sources);
            searchCommand.Options.Add(Take);
            searchCommand.Options.Add(Skip);
            searchCommand.Options.Add(ExactMatch);
            searchCommand.Options.Add(Interactive);
            searchCommand.Options.Add(Prerelease);
            searchCommand.Options.Add(ConfigFile);
            searchCommand.Options.Add(Format);
            searchCommand.Options.Add(Verbosity);

            searchCommand.SetAction((parseResult) => {
                var command = new PackageSearchCommand(parseResult);
                int exitCode = command.Execute();

                if (exitCode == 1)
                {
                    parseResult.ShowHelp();
                }
                // Only return 1 or 0
                return exitCode == 0 ? 0 : 1;
            });

            return searchCommand;
        }
    }
}
