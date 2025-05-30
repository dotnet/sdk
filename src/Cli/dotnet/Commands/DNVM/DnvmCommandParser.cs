// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.DNVM;

internal static class DnvmCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-dnvm";

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        DocumentedCommand command = new("dnvm", DocsLink, "The .NET version manager");
        command.Subcommands.Add(InstallCommandParser.GetCommand());
        command.Subcommands.Add(UninstallCommandParser.GetCommand());

        command.SetAction((parseResult) => parseResult.HandleMissingCommand());

        return command;
    }
}
