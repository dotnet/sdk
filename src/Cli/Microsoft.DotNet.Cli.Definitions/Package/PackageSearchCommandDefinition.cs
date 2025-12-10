// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Package.Search;

internal static class PackageSearchCommandDefinition
{
    public const string Name = "search";

    public static readonly Argument<string> SearchTermArgument = new Argument<string>("SearchTerm")
    {
        HelpName = CliCommandStrings.PackageSearchSearchTermArgumentName,
        Description = CliCommandStrings.PackageSearchSearchTermDescription,
        Arity = ArgumentArity.ZeroOrOne
    };

    public static readonly Option Sources = new Option<IEnumerable<string>>("--source")
    {
        Description = CliCommandStrings.SourceDescription,
        HelpName = CliCommandStrings.SourceArgumentName
    }.ForwardAsManyArgumentsEachPrefixedByOption("--source")
    .AllowSingleArgPerToken();

    public static readonly Option<string> Take = new Option<string>("--take")
    {
        Description = CliCommandStrings.PackageSearchTakeDescription,
        HelpName = CliCommandStrings.PackageSearchTakeArgumentName
    }.ForwardAsSingle(o => $"--take:{o}");

    public static readonly Option<string> Skip = new Option<string>("--skip")
    {
        Description = CliCommandStrings.PackageSearchSkipDescription,
        HelpName = CliCommandStrings.PackageSearchSkipArgumentName
    }.ForwardAsSingle(o => $"--skip:{o}");

    public static readonly Option<bool> ExactMatch = new Option<bool>("--exact-match")
    {
        Description = CliCommandStrings.ExactMatchDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("--exact-match");

    public static readonly Option<bool> Interactive = CommonOptions.InteractiveOption().ForwardIfEnabled("--interactive");

    public static readonly Option<bool> Prerelease = new Option<bool>("--prerelease")
    {
        Description = CliCommandStrings.PackageSearchPrereleaseDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("--prerelease");

    public static readonly Option<string> ConfigFile = new Option<string>("--configfile")
    {
        Description = CliCommandStrings.ConfigFileDescription,
        HelpName = CliCommandStrings.ConfigFileArgumentName
    }.ForwardAsSingle(o => $"--configfile:{o}");

    public static readonly Option<string> Format = new Option<string>("--format")
    {
        Description = CliCommandStrings.FormatDescription,
        HelpName = CliCommandStrings.FormatArgumentName
    }.ForwardAsSingle(o => $"--format:{o}");

    public static readonly Option<string> Verbosity = new Option<string>("--verbosity")
    {
        Description = CliCommandStrings.VerbosityDescription,
        HelpName = CliCommandStrings.VerbosityArgumentName
    }.ForwardAsSingle(o => $"--verbosity:{o}");

    public static readonly IEnumerable<Option> Options =
    [
        Sources,
        Take,
        Skip,
        ExactMatch,
        Interactive,
        Prerelease,
        ConfigFile,
        Format,
        Verbosity
    ];

    public static Command Create()
    {
        Command searchCommand = new(Name, CliCommandStrings.PackageSearchCommandDescription);

        searchCommand.Arguments.Add(SearchTermArgument);
        searchCommand.Options.AddRange(Options);

        return searchCommand;
    }
}
