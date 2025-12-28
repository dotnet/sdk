// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Tool.Search;

internal sealed class ToolSearchCommandDefinition : Command
{
    public readonly Argument<string> SearchTermArgument = new("searchTerm")
    {
        HelpName = CliCommandStrings.ToolSearchSearchTermArgumentName,
        Description = CliCommandStrings.ToolSearchSearchTermDescription
    };

    public readonly Option<bool> DetailOption = new("--detail")
    {
        Description = CliCommandStrings.DetailDescription,
        Arity = ArgumentArity.Zero
    };

    public readonly Option<string> SkipOption = new("--skip")
    {
        Description = CliCommandStrings.ToolSearchSkipDescription,
        HelpName = CliCommandStrings.ToolSearchSkipArgumentName
    };

    public readonly Option<string> TakeOption = new("--take")
    {
        Description = CliCommandStrings.ToolSearchTakeDescription,
        HelpName = CliCommandStrings.ToolSearchTakeArgumentName
    };

    public readonly Option<bool> PrereleaseOption = ToolAppliedOption.CreatePrereleaseOption();

    public ToolSearchCommandDefinition()
        : base("search", CliCommandStrings.ToolSearchCommandDescription)
    {
        Arguments.Add(SearchTermArgument);

        Options.Add(DetailOption);
        Options.Add(SkipOption);
        Options.Add(TakeOption);
        Options.Add(PrereleaseOption);
    }
}
