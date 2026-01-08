// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Solution.Migrate;

public static class SolutionMigrateCommandDefinition
{
    public const string Name = "migrate";

    public static Command Create()
    {
        Command command = new(Name, CliCommandStrings.MigrateAppFullName);
        command.Arguments.Add(SolutionCommandDefinition.SlnArgument);
        return command;
    }
}
