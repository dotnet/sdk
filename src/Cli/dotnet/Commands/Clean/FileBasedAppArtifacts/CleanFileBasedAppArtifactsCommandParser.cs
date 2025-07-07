// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Clean.FileBasedAppArtifacts;

internal sealed class CleanFileBasedAppArtifactsCommandParser
{
    public static readonly Option<bool> DryRunOption = new("--dry-run")
    {
        Description = CliCommandStrings.CleanFileBasedAppArtifactsDryRun,
        Arity = ArgumentArity.Zero,
    };

    public static readonly Option<int> DaysOption = new("--days")
    {
        Description = CliCommandStrings.CleanFileBasedAppArtifactsDays,
        DefaultValueFactory = _ => 30,
    };

    public static Command GetCommand()
    {
        Command command = new("clean-file-based-app-artifacts", CliCommandStrings.CleanFileBasedAppArtifactsCommandDescription)
        {
            Hidden = true,
            Options =
            {
                DryRunOption,
                DaysOption,
            },
        };

        command.SetAction((parseResult) => new CleanFileBasedAppArtifactsCommand(parseResult).Execute());
        return command;
    }
}
