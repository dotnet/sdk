// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Reference.List;

internal static class ReferenceListCommandParser
{
    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        var command = new CliCommand("list", CliCommandStrings.ReferenceListAppFullName);

        command.SetAction((parseResult) => new ReferenceListCommand(parseResult).Execute());

        return command;
    }
}
