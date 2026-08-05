// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Sdk.Add;
using Microsoft.DotNet.Cli.Commands.Sdk.Check;
using Microsoft.DotNet.Cli.Commands.Sdk.Remove;

namespace Microsoft.DotNet.Cli.Commands.Sdk;

internal sealed class SdkCommandDefinition : Command
{
    private const string Link = "https://aka.ms/dotnet-sdk";

    public readonly SdkCheckCommandDefinition CheckCommand = new();
    public readonly SdkAddCommandDefinition AddCommand = new();
    public readonly SdkRemoveCommandDefinition RemoveCommand = new();

    public SdkCommandDefinition()
        : base("sdk", CommandDefinitionStrings.SdkAppFullName)
    {
        this.DocsLink = Link;
        Subcommands.Add(CheckCommand);
        Subcommands.Add(AddCommand);
        Subcommands.Add(RemoveCommand);
    }
}
