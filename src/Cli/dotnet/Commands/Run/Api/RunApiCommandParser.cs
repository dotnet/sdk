// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Run.Api;

internal sealed class RunApiCommandParser
{
    public static Command GetCommand()
    {
        Command command = new("run-api")
        {
            Hidden = true,
        };

        command.SetAction((parseResult) => new RunApiCommand(parseResult).Execute());
        return command;
    }
}
