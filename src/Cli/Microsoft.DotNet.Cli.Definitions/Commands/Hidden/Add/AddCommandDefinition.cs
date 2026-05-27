// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Hidden.Add.Package;
using Microsoft.DotNet.Cli.Commands.Hidden.Add.Reference;
using Microsoft.DotNet.Cli.Commands.Package;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Add;

internal sealed class AddCommandDefinition : Command
{
    public new const string Name = "add";
    private const string Link = "https://aka.ms/dotnet-add";

    public readonly AddPackageCommandDefinition PackageCommand = new();
    public readonly AddReferenceCommandDefinition ReferenceCommand = new();

    public readonly Argument<string> ProjectOrFileArgument = PackageCommandDefinition.CreateProjectOrFileArgument();

    public AddCommandDefinition()
        : base(Name, CommandDefinitionStrings.NetAddCommand)
    {
        Hidden = true;
        this.DocsLink = Link;

        Arguments.Add(ProjectOrFileArgument);
        Subcommands.Add(PackageCommand);
        Subcommands.Add(ReferenceCommand);
    }
}
