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

    /// <summary>
    /// Specified internally when the command is started automatically in background by <c>dotnet run</c>.
    /// Causes <see cref="RunFileArtifactsMetadata.LastAutomaticCleanupUtc"/> to be updated.
    /// </summary>
    public static readonly Option<bool> AutomaticOption = new("--automatic")
    {
        Hidden = true,
    };

    public static Command Command => field ??= ConstructCommand();

    private static Command ConstructCommand()
    {
        Command command = new("file-based-apps", CliCommandStrings.CleanFileBasedAppArtifactsCommandDescription)
        {
            Hidden = true,
            Options =
            {
                DryRunOption,
                DaysOption,
                AutomaticOption,
            },
        };

        command.SetAction((parseResult) => new CleanFileBasedAppArtifactsCommand(parseResult).Execute());
        return command;
    }
}
