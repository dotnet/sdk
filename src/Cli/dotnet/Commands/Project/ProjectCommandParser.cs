// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Project.Convert;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Project;

internal sealed class ProjectCommandParser
{
    private static readonly Command Command = ConfigureCommand(ProjectCommandDefinition.Create());

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConfigureCommand(Command command)
    {
        command.SetAction((parseResult) => parseResult.HandleMissingCommand());

        command.Subcommands.Single(c => c.Name == ProjectConvertCommandDefinition.Name).SetAction(parseResult => new ProjectConvertCommand(parseResult).Execute());

        return command;
    }
}
