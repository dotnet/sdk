// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Tool.Restore;

internal sealed class ToolRestoreCommandDefinition : Command
{
    public readonly Option<string> ConfigOption = ToolAppliedOption.CreateConfigOption();

    public readonly Option<string[]> AddSourceOption = ToolAppliedOption.CreateAddSourceOption();

    public readonly Option<string> ToolManifestOption = ToolAppliedOption.CreateToolManifestOption(CommandDefinitionStrings.ToolRestoreManifestPathOptionDescription);

    public readonly Option<Utils.VerbosityOptions> VerbosityOption = CommonOptions.CreateVerbosityOption(Utils.VerbosityOptions.normal);

    public readonly NuGetRestoreOptions RestoreOptions = new(forward: true);

    public ToolRestoreCommandDefinition()
        : base("restore", CommandDefinitionStrings.ToolRestoreCommandDescription)
    {
        Options.Add(ConfigOption);
        Options.Add(AddSourceOption);
        Options.Add(ToolManifestOption);
        Options.Add(VerbosityOption);

        RestoreOptions.AddTo(Options);
    }
}
