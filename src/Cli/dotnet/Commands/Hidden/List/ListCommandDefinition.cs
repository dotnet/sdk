// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Hidden.List.Package;
using Microsoft.DotNet.Cli.Commands.Hidden.List.Reference;

namespace Microsoft.DotNet.Cli.Commands.Hidden.List;

internal static class ListCommandDefinition
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-list";

    public static readonly Argument<string> SlnOrProjectArgument = CreateSlnOrProjectArgument(CliStrings.SolutionOrProjectArgumentName, CliStrings.SolutionOrProjectArgumentDescription);

    internal static Argument<string> CreateSlnOrProjectArgument(string name, string description)
        => new Argument<string>(name)
        {
            Description = description,
            Arity = ArgumentArity.ZeroOrOne
        }.DefaultToCurrentDirectory();

    public static Command Create()
    {
        var command = new Command("list", CliCommandStrings.NetListCommand)
        {
            Hidden = true,
            DocsLink = DocsLink
        };

        command.Arguments.Add(SlnOrProjectArgument);
        command.Subcommands.Add(ListPackageCommandDefinition.Create());
        command.Subcommands.Add(ListReferenceCommandDefinition.Create());

        return command;
    }
}
