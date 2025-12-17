// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Project.Convert;

namespace Microsoft.DotNet.Cli.Commands.Project;

internal sealed class ProjectCommandDefinition
{
    public static Command Create()
    {
        Command command = new("project");
        command.Subcommands.Add(ProjectConvertCommandDefinition.Create());

        return command;
    }
}
