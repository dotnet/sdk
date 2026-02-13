// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Hidden.Remove.Package;
using Microsoft.DotNet.Cli.Commands.Hidden.Remove.Reference;
using Microsoft.DotNet.Cli.Commands.Package;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Remove;

internal sealed class RemoveCommandDefinition : Command
{
    public new const string Name = "remove";

    private const string Link = "https://aka.ms/dotnet-remove";

    public readonly Argument<string> ProjectOrFileArgument = PackageCommandDefinition.CreateProjectOrFileArgument();

    public readonly RemovePackageCommandDefinition PackageCommand = new();
    public readonly RemoveReferenceCommandDefinition ReferenceCommand = new();

    public RemoveCommandDefinition()
        : base(Name, CommandDefinitionStrings.NetRemoveCommand)
    {
        Hidden = true;
        this.DocsLink = Link;

        Arguments.Add(ProjectOrFileArgument);
        Subcommands.Add(PackageCommand);
        Subcommands.Add(ReferenceCommand);
    }
}
