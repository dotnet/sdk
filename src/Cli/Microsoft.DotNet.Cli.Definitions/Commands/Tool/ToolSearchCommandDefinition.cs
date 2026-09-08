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

    public readonly Option<FileInfo> ConfigOption = ToolAppliedOption.CreateConfigOption();

    public readonly Option<string[]> SourceOption = ToolAppliedOption.CreateSourceOption(CommandDefinitionStrings.SourceDescription);

    public readonly Option<string[]> AddSourceOption = ToolAppliedOption.CreateAddSourceOption(CommandDefinitionStrings.Option_AddSource);

    public readonly Option<bool> InteractiveOption = CommonOptions.CreateInteractiveOption();

    public ToolSearchCommandDefinition()
        : base("search", CommandDefinitionStrings.ToolSearchCommandDescription)
    {
        Arguments.Add(SearchTermArgument);

        Options.Add(DetailOption);
        Options.Add(SkipOption);
        Options.Add(TakeOption);
        Options.Add(PrereleaseOption);
        Options.Add(ConfigOption);
        Options.Add(SourceOption);
        Options.Add(AddSourceOption);
        Options.Add(InteractiveOption);
    }
}
