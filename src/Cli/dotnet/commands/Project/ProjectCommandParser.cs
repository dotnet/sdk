// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli;

internal sealed class ProjectCommandParser
{
    public static CliCommand GetCommand()
    {
        CliCommand command = new("project");
        command.SetAction((parseResult) => parseResult.HandleMissingCommand());
        command.Subcommands.Add(ProjectConvertCommandParser.GetCommand());

        return command;
    }
}
