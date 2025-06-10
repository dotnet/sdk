// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Hidden.List.Package;
using Microsoft.DotNet.Cli.Commands.Hidden.List.Reference;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Hidden.List;

internal static class ListCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-list";

    public static readonly Argument<string> SlnOrProjectArgument = CreateSlnOrProjectArgument(CliStrings.SolutionOrProjectArgumentName, CliStrings.SolutionOrProjectArgumentDescription);

    internal static Argument<string> CreateSlnOrProjectArgument(string name, string description)
        => new Argument<string>(name)
        {
            Description = description,
            Arity = ArgumentArity.ZeroOrOne
        }.DefaultToCurrentDirectory();

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        var command = new DocumentedCommand("list", DocsLink, CliCommandStrings.NetListCommand)
        {
            Hidden = true
        };

        command.Arguments.Add(SlnOrProjectArgument);
        command.Subcommands.Add(ListPackageCommandParser.GetCommand());
        command.Subcommands.Add(ListReferenceCommandParser.GetCommand());

        command.SetAction((parseResult) => parseResult.HandleMissingCommand());

        return command;
    }
}
