// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Reference;
using Microsoft.DotNet.Cli.Commands.Reference.Add;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Add.Reference;

internal static class AddReferenceCommandParser
{
    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = new("reference", CliCommandStrings.ReferenceAddAppFullName);

        command.Arguments.Add(ReferenceAddCommandParser.ProjectPathArgument);
        command.Options.Add(ReferenceAddCommandParser.FrameworkOption);
        command.Options.Add(ReferenceAddCommandParser.InteractiveOption);
        command.Options.Add(ReferenceCommandParser.ProjectOption);

        command.SetAction((parseResult) => new ReferenceAddCommand(parseResult).Execute());

        return command;
    }
}
