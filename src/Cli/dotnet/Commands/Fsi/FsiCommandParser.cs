// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Fsi;

internal static class FsiCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-fsi";

    public static readonly Argument<string[]> Arguments = new("arguments");

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        DocumentedCommand command = new("fsi", DocsLink) { Arguments };

        command.SetAction((parseResult) => FsiCommand.Run(parseResult.GetValue(Arguments)));

        return command;
    }
}
