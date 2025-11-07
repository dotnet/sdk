// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Fsi;

internal static partial class FsiCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-fsi";

    public static readonly Argument<string[]> Arguments = new("arguments");

    public static Command CreateCommandDefinition()
    {
        Command command = new("fsi") {
            Arguments = { Arguments },
            DocsLink = DocsLink,
        };
        command.SetAction((parseResult) => FsiCommand.Run(parseResult.GetValue(Arguments)));

        return command;
    }
}
