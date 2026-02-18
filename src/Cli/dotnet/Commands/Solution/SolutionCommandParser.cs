// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Solution.Add;
using Microsoft.DotNet.Cli.Commands.Solution.List;
using Microsoft.DotNet.Cli.Commands.Solution.Migrate;
using Microsoft.DotNet.Cli.Commands.Solution.Remove;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Solution;

internal static class SolutionCommandParser
{
    public static void ConfigureCommand(SolutionCommandDefinition command)
    {
        command.SetAction(parseResult => parseResult.HandleMissingCommand());

        command.AddCommand.SetAction(parseResult => new SolutionAddCommand(parseResult).Execute());
        command.ListCommand.SetAction(parseResult => new SolutionListCommand(parseResult).Execute());
        command.MigrateCommand.SetAction(parseResult => new SolutionMigrateCommand(parseResult).Execute());
        command.RemoveCommand.SetAction(parseResult => new SolutionRemoveCommand(parseResult).Execute());
    }
}
