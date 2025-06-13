// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;

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
        var formatCommand = new DocumentedCommand("format", DocsLink)
        {
            Arguments
        };
        formatCommand.SetAction((parseResult) => FormatCommand.Run(parseResult.GetValue(Arguments)));
        return formatCommand;
    }
}
