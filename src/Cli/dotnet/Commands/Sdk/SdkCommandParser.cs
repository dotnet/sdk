// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Sdk.Check;
using Microsoft.DotNet.Cli.Extensions;
using LocalizableStrings = Microsoft.DotNet.Tools.Sdk.LocalizableStrings;

namespace Microsoft.DotNet.Cli.Commands.Sdk;

internal static class SdkCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-sdk";

    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        DocumentedCommand command = new("sdk", DocsLink, LocalizableStrings.AppFullName);
        command.Subcommands.Add(SdkCheckCommandParser.GetCommand());

        command.SetAction((parseResult) => parseResult.HandleMissingCommand());

        return command;
    }
}
