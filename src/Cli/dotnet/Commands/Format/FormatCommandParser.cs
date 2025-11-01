// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Format;

internal static partial class FormatCommandParser
{
    public static readonly Argument<string[]> Arguments = new("arguments");

    public static readonly string DocsLink = "https://aka.ms/dotnet-format";

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        var formatCommand = new Command("format")
        {
            Arguments = { Arguments },
            DocsLink = DocsLink,
        };
        formatCommand.SetAction((parseResult) => FormatCommand.Run(parseResult.GetValue(Arguments)));
        return formatCommand;
    }
}
