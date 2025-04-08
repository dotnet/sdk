// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Reference.Remove;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Remove.Reference;

internal static class RemoveReferenceCommandParser
{
    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        var command = new CliCommand("reference", CliCommandStrings.ReferenceRemoveAppFullName);

        command.Arguments.Add(ReferenceRemoveCommandParser.ProjectPathArgument);
        command.Options.Add(ReferenceRemoveCommandParser.FrameworkOption);

        command.SetAction((parseResult) => new ReferenceRemoveCommand(parseResult).Execute());

        return command;
    }
}
