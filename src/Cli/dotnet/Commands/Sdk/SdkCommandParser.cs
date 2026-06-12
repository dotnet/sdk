// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Sdk.Check;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Sdk;

internal static class SdkCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-sdk";

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        DocumentedCommand command = new("sdk", DocsLink, CliCommandStrings.SdkAppFullName);
        command.Subcommands.Add(SdkCheckCommandParser.GetCommand());

        command.SetAction((parseResult) => parseResult.HandleMissingCommand());

        return command;
    }
}
