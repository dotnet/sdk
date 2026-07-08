// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Package.Add;
using Microsoft.DotNet.Cli.Commands.Package.List;
using Microsoft.DotNet.Cli.Commands.Package.Remove;
using Microsoft.DotNet.Cli.Commands.Package.Search;
using Command = System.CommandLine.Command;

namespace Microsoft.DotNet.Cli.Commands.Package;

internal sealed class PackageCommandDefinition : Command
{
    public new const string Name = "package";
    private const string Link = "https://aka.ms/dotnet-package";

    public static Option<string?> CreateProjectOption() => new("--project")
    {
        Recursive = true,
        Description = CommandDefinitionStrings.ProjectArgumentDescription
    };

    public static Option<string?> CreateFileOption() => new("--file")
    {
        Recursive = true,
        Description = CommandDefinitionStrings.FileArgumentDescription
    };

    // Used by the legacy 'add/remove package' commands.
    public static Argument<string> CreateProjectOrFileArgument() => new Argument<string>(CommandDefinitionStrings.ProjectOrFileArgumentName)
    {
        Description = CommandDefinitionStrings.ProjectOrFileArgumentDescription
    }.DefaultToCurrentDirectory();

    public readonly PackageSearchCommandDefinition SearchCommand = new();
    public readonly PackageAddCommandDefinition AddCommand = new();
    public readonly PackageListCommandDefinition ListCommand = new();
    public readonly PackageRemoveCommandDefinition RemoveCommand = new();

    public PackageCommandDefinition()
        : base(Name)
    {
        this.DocsLink = Link;

        Subcommands.Add(SearchCommand);
        Subcommands.Add(AddCommand);
        Subcommands.Add(ListCommand);
        Subcommands.Add(RemoveCommand);
    }
}
