// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Clean.FileBasedAppArtifacts;

internal sealed class CleanFileBasedAppArtifactsCommandDefinition : Command
{
    public new const string Name = "file-based-apps";

    public readonly Option<bool> DryRunOption = new("--dry-run")
    {
        Description = CliCommandStrings.CleanFileBasedAppArtifactsDryRun,
        Arity = ArgumentArity.Zero,
    };

    public readonly Option<int> DaysOption = new("--days")
    {
        Description = CliCommandStrings.CleanFileBasedAppArtifactsDays,
        DefaultValueFactory = _ => 30,
    };

    public const string AutomaticOptionName = "--automatic";

    /// <summary>
    /// Specified internally when the command is started automatically in background by <c>dotnet run</c>.
    /// Causes <see cref="RunFileArtifactsMetadata.LastAutomaticCleanupUtc"/> to be updated.
    /// </summary>
    public readonly Option<bool> AutomaticOption = new(AutomaticOptionName)
    {
        Hidden = true,
    };

    public CleanFileBasedAppArtifactsCommandDefinition()
        : base(Name, CliCommandStrings.CleanFileBasedAppArtifactsCommandDescription)
    {
        Hidden = true;
        Options.Add(DryRunOption);
        Options.Add(DaysOption);
        Options.Add(AutomaticOption);
    }
}
