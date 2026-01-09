// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Hidden.List.Package;
using Microsoft.DotNet.Cli.Commands.Hidden.List.Reference;

namespace Microsoft.DotNet.Cli.Commands.Hidden.List;

internal sealed class ListCommandDefinition : Command
{
    private new const string Name = "list";
    private const string Link = "https://aka.ms/dotnet-list";

    public static Argument<string> CreateSlnOrProjectArgument(string name, string description)
        => new Argument<string>(name)
        {
            Description = description,
            Arity = ArgumentArity.ZeroOrOne
        }.DefaultToCurrentDirectory();

    public readonly Argument<string> SlnOrProjectArgument = CreateSlnOrProjectArgument(CommandDefinitionStrings.SolutionOrProjectArgumentName, CommandDefinitionStrings.SolutionOrProjectArgumentDescription);

    public readonly ListPackageCommandDefinition PackageCommand = new();
    public readonly ListReferenceCommandDefinition ReferenceCommand = new();

    public ListCommandDefinition()
        : base(Name, CommandDefinitionStrings.NetListCommand)
    {
        Hidden = true;
        this.DocsLink = Link;

        Arguments.Add(SlnOrProjectArgument);
        Subcommands.Add(PackageCommand);
        Subcommands.Add(ReferenceCommand);
    }
}
