// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Reference;
using Microsoft.DotNet.Cli.Commands.Reference.Add;

namespace Microsoft.DotNet.Cli.Commands.Add.Reference;

internal static class AddProjectToProjectReferenceParser
{
    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        CliCommand command = new("reference", CliCommandStrings.ReferenceAddAppFullName);

        command.Arguments.Add(ReferenceAddCommandParser.ProjectPathArgument);
        command.Options.Add(ReferenceAddCommandParser.FrameworkOption);
        command.Options.Add(ReferenceAddCommandParser.InteractiveOption);
        command.Options.Add(ReferenceCommandParser.ProjectOption);

        command.SetAction((parseResult) => new AddProjectToProjectReferenceCommand(parseResult).Execute());

        return command;
    }
}
