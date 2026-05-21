// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
#if !CLI_AOT
using Microsoft.DotNet.Cli.Commands.Solution.Add;
using Microsoft.DotNet.Cli.Extensions;
#endif
using Microsoft.DotNet.Cli.Commands.Solution.List;
using Microsoft.DotNet.Cli.Commands.Solution.Migrate;
using Microsoft.DotNet.Cli.Commands.Solution.Remove;

namespace Microsoft.DotNet.Cli.Commands.Solution;

internal static class SolutionCommandParser
{
    public static void ConfigureCommand(SolutionCommandDefinition command)
    {
#if CLI_AOT
        // In AOT mode, 'sln add' requires MSBuild and bare 'dotnet sln' needs full help —
        // both require fallback to the managed CLI.
        command.SetAction((Func<ParseResult, int>)(_ => throw new CommandNotAvailableInAotException()));
        command.AddCommand.SetAction((Func<ParseResult, int>)(_ => throw new CommandNotAvailableInAotException()));
#else
        command.SetAction(parseResult => parseResult.HandleMissingCommand());
        command.AddCommand.SetAction(parseResult => new SolutionAddCommand(parseResult).Execute());
#endif
        command.ListCommand.SetAction(parseResult => new SolutionListCommand(parseResult).Execute());
        command.MigrateCommand.SetAction(parseResult => new SolutionMigrateCommand(parseResult).Execute());
        command.RemoveCommand.SetAction(parseResult => new SolutionRemoveCommand(parseResult).Execute());
    }
}
