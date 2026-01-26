// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Tool;

internal abstract class ToolUpdateInstallCommandDefinition : System.CommandLine.Command
{
    public readonly Option<bool> AllowPackageDowngradeOption = ToolAppliedOption.CreateAllowPackageDowngradeOption();

    public readonly ToolLocationOptions LocationOptions = new(
        globalOptionDescription: CommandDefinitionStrings.ToolInstallGlobalOptionDescription,
        localOptionDescription: CommandDefinitionStrings.ToolInstallLocalOptionDescription,
        toolPathOptionDescription: CommandDefinitionStrings.ToolInstallToolPathOptionDescription);

    public readonly Option<string> VersionOption = ToolAppliedOption.CreateVersionOption();
    public readonly Option<string> ConfigOption = ToolAppliedOption.CreateConfigOption();
    public readonly Option<string> ToolManifestOption = ToolAppliedOption.CreateToolManifestOption(CommandDefinitionStrings.ToolInstallManifestPathOptionDescription);

    public readonly Option<string[]> AddSourceOption = ToolAppliedOption.CreateAddSourceOption();
    public readonly Option<string[]> SourceOption = ToolAppliedOption.CreateSourceOption();

    public readonly Option<string> FrameworkOption = new("--framework")
    {
        Description = CommandDefinitionStrings.ToolInstallFrameworkOptionDescription,
        HelpName = CommandDefinitionStrings.ToolInstallFrameworkOptionName
    };

    public readonly Option<bool> PrereleaseOption = ToolAppliedOption.CreatePrereleaseOption();

    public readonly NuGetRestoreOptions RestoreOptions = new(forward: true);

    public readonly Option<VerbosityOptions> VerbosityOption = CommonOptions.CreateVerbosityOption(VerbosityOptions.normal);

    public ToolUpdateInstallCommandDefinition(string name, string description)
        : base(name, description)
    {
        LocationOptions.AddTo(Options);

        Options.Add(VersionOption);
        Options.Add(ConfigOption);
        Options.Add(ToolManifestOption);
        Options.Add(AddSourceOption);
        Options.Add(SourceOption);
        Options.Add(FrameworkOption);
        Options.Add(PrereleaseOption);

        RestoreOptions.AddTo(Options);

        Options.Add(VerbosityOption);
        Options.Add(AllowPackageDowngradeOption);
    }
}
