// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Extensions;
#if !CLI_AOT
using Microsoft.DotNet.Cli.Commands.Solution.Add;
#endif
using Microsoft.DotNet.Cli.Commands.Solution.List;
using Microsoft.DotNet.Cli.Commands.Solution.Migrate;
using Microsoft.DotNet.Cli.Commands.Solution.Remove;

namespace Microsoft.DotNet.Cli.Commands.Solution;

internal static class SolutionCommandParser
{
    public static void ConfigureCommand(SolutionCommandDefinition command)
    {
        command.SetAction(parseResult => parseResult.HandleMissingCommand());
#if CLI_AOT
        // 'sln add' requires MSBuild, so it falls back to the managed CLI.
        command.AddCommand.SetAction((Func<ParseResult, int>)(_ => throw new CommandNotAvailableInAotException()));
#else
        command.AddCommand.SetAction(parseResult => new SolutionAddCommand(parseResult).Execute());
#endif
        command.ListCommand.SetAction(parseResult => new SolutionListCommand(parseResult).Execute());
        command.MigrateCommand.SetAction(parseResult => new SolutionMigrateCommand(parseResult).Execute());
        command.RemoveCommand.SetAction(parseResult => new SolutionRemoveCommand(parseResult).Execute());
    }
}
