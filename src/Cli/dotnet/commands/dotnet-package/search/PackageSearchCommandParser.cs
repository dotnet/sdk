// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.NuGet;
using LocalizableStrings = Microsoft.DotNet.Tools.Package.Search.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class PackageSearchCommandParser
    {
        public static readonly CliArgument<IEnumerable<string>> SearchTermArgument = new CliArgument<IEnumerable<string>>("SearchTerm")
        {
            HelpName = LocalizableStrings.SearchTermArgumentName,
            Description = LocalizableStrings.SearchTermDescription
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
        }.ForwardAsMany(o => new [] { "--take", o });

        public static readonly CliOption<string> Skip = new ForwardedOption<string>("--skip")
        {
            Description = LocalizableStrings.SkipDescription,
            HelpName = LocalizableStrings.SkipArgumentName
        }.ForwardAsMany(o => new[] { "--skip", o });

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


        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
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

            searchCommand.SetAction((parseResult) => new PackageSearchCommand(parseResult).Execute());

            return searchCommand;
        }
    }
}
