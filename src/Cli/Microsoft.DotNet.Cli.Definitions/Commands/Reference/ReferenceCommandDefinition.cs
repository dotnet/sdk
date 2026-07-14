// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Reference.Add;
using Microsoft.DotNet.Cli.Commands.Reference.List;
using Microsoft.DotNet.Cli.Commands.Reference.Remove;

namespace Microsoft.DotNet.Cli.Commands.Reference;

internal sealed class ReferenceCommandDefinition : Command
{
    public new const string Name = "reference";
    private const string Link = "https://aka.ms/dotnet-reference";

    public static Option<string> CreateProjectOption() => new("--project")
    {
        Description = CommandDefinitionStrings.ProjectArgumentDescription,
        Recursive = true
    };

    public readonly Option<string> ProjectOption = CreateProjectOption();

    public readonly ReferenceAddCommandDefinition AddCommand = new();
    public readonly ReferenceListCommandDefinition ListCommand = new();
    public readonly ReferenceRemoveCommandDefinition RemoveCommand = new();

    public ReferenceCommandDefinition()
        : base(Name, CommandDefinitionStrings.NetRemoveCommand)
    {
        this.DocsLink = Link;

        Subcommands.Add(AddCommand);
        Subcommands.Add(ListCommand);
        Subcommands.Add(RemoveCommand);
        Options.Add(ProjectOption);
    }
}
