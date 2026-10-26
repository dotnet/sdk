// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Tool.Search;

internal sealed class ToolSearchCommandDefinition : Command
{
    public readonly Argument<string> SearchTermArgument = new("searchTerm")
    {
        HelpName = CommandDefinitionStrings.ToolSearchSearchTermArgumentName,
        Description = CommandDefinitionStrings.ToolSearchSearchTermDescription
    };

    public readonly Option<bool> DetailOption = new("--detail")
    {
        Description = CommandDefinitionStrings.DetailDescription,
        Arity = ArgumentArity.Zero
    };

    public readonly Option<string> SkipOption = new("--skip")
    {
        Description = CommandDefinitionStrings.ToolSearchSkipDescription,
        HelpName = CommandDefinitionStrings.ToolSearchSkipArgumentName
    };

    public readonly Option<string> TakeOption = new("--take")
    {
        Description = CommandDefinitionStrings.ToolSearchTakeDescription,
        HelpName = CommandDefinitionStrings.ToolSearchTakeArgumentName
    };

    public readonly Option<bool> PrereleaseOption = ToolAppliedOption.CreatePrereleaseOption();

    public ToolSearchCommandDefinition()
        : base("search", CommandDefinitionStrings.ToolSearchCommandDescription)
    {
        Arguments.Add(SearchTermArgument);

        Options.Add(DetailOption);
        Options.Add(SkipOption);
        Options.Add(TakeOption);
        Options.Add(PrereleaseOption);
    }
}
