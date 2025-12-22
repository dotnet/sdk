// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.MSBuild;

internal static class MSBuildCommandParser
{
    private static readonly Command Command = ConfigureCommand(MSBuildCommandDefinition.Create());

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConfigureCommand(Command command)
    {
        command.SetAction(MSBuildCommand.Run);
        return command;
    }
}
