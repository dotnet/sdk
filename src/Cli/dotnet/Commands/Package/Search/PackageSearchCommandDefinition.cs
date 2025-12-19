// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Package.Search;

internal sealed class PackageSearchCommandDefinition : Command
{
    public new const string Name = "search";

    public readonly Argument<string> SearchTermArgument = new("SearchTerm")
    {
        HelpName = CliCommandStrings.PackageSearchSearchTermArgumentName,
        Description = CliCommandStrings.PackageSearchSearchTermDescription,
        Arity = ArgumentArity.ZeroOrOne
    };

    public readonly Option<IEnumerable<string>> Sources = new Option<IEnumerable<string>>("--source")
    {
        Description = CliCommandStrings.SourceDescription,
        HelpName = CliCommandStrings.SourceArgumentName
    }.ForwardAsManyArgumentsEachPrefixedByOption("--source")
    .AllowSingleArgPerToken();

    public readonly Option<string> Take = new Option<string>("--take")
    {
        Description = CliCommandStrings.PackageSearchTakeDescription,
        HelpName = CliCommandStrings.PackageSearchTakeArgumentName
    }.ForwardAsSingle(o => $"--take:{o}");

    public readonly Option<string> Skip = new Option<string>("--skip")
    {
        Description = CliCommandStrings.PackageSearchSkipDescription,
        HelpName = CliCommandStrings.PackageSearchSkipArgumentName
    }.ForwardAsSingle(o => $"--skip:{o}");

    public readonly Option<bool> ExactMatch = new Option<bool>("--exact-match")
    {
        Description = CliCommandStrings.ExactMatchDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("--exact-match");

    public readonly Option<bool> Interactive = CommonOptions.InteractiveOption().ForwardIfEnabled("--interactive");

    public readonly Option<bool> Prerelease = new Option<bool>("--prerelease")
    {
        Description = CliCommandStrings.PackageSearchPrereleaseDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("--prerelease");

    public readonly Option<string> ConfigFile = new Option<string>("--configfile")
    {
        Description = CliCommandStrings.ConfigFileDescription,
        HelpName = CliCommandStrings.ConfigFileArgumentName
    }.ForwardAsSingle(o => $"--configfile:{o}");

    public readonly Option<string> Format = new Option<string>("--format")
    {
        Description = CliCommandStrings.FormatDescription,
        HelpName = CliCommandStrings.FormatArgumentName
    }.ForwardAsSingle(o => $"--format:{o}");

    public readonly Option<string> Verbosity = new Option<string>("--verbosity")
    {
        Description = CliCommandStrings.VerbosityDescription,
        HelpName = CliCommandStrings.VerbosityArgumentName
    }.ForwardAsSingle(o => $"--verbosity:{o}");

    public PackageSearchCommandDefinition()
        : base(Name, CliCommandStrings.PackageSearchCommandDescription)
    {
        Arguments.Add(SearchTermArgument);
        Options.Add(Sources);
        Options.Add(Take);
        Options.Add(Skip);
        Options.Add(ExactMatch);
        Options.Add(Interactive);
        Options.Add(Prerelease);
        Options.Add(ConfigFile);
        Options.Add(Format);
        Options.Add(Verbosity);
    }
}
