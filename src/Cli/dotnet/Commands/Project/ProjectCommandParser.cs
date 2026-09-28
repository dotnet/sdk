// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Project.Convert;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Project;

internal sealed class ProjectCommandParser
{
    public static Command GetCommand()
    {
        Command command = new("project");
        command.SetAction((parseResult) => parseResult.HandleMissingCommand());
        command.Subcommands.Add(ProjectConvertCommandParser.GetCommand());

        return command;
    }
}
