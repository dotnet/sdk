// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Tool.Execute;

internal sealed class ToolExecuteCommandDefinition : ToolExecuteCommandDefinitionBase
{
    public ToolExecuteCommandDefinition()
        : base("execute")
    {
        Aliases.Add("exec");
    }
}

internal abstract class ToolExecuteCommandDefinitionBase : Command
{
    public readonly Argument<PackageIdentityWithRange> PackageIdentityArgument = CommonArguments.CreateRequiredPackageIdentityArgument("dotnetsay", "2.1.7");

    public readonly Argument<IEnumerable<string>> CommandArgument = new("commandArguments")
    {
        Description = CommandDefinitionStrings.ToolRunArgumentsDescription
    };

    public readonly Option<string> VersionOption = ToolAppliedOption.CreateVersionOption();
    public readonly Option<bool> RollForwardOption = ToolAppliedOption.CreateRollForwardOption();
    public readonly Option<bool> PrereleaseOption = ToolAppliedOption.CreatePrereleaseOption();
    public readonly Option<string> ConfigOption = ToolAppliedOption.CreateConfigOption();
    public readonly Option<string[]> SourceOption = ToolAppliedOption.CreateSourceOption();
    public readonly Option<string[]> AddSourceOption = ToolAppliedOption.CreateAddSourceOption();
    public readonly Option<bool> YesOption = CommonOptions.CreateYesOption();
    public readonly Option<Utils.VerbosityOptions> VerbosityOption = CommonOptions.CreateVerbosityOption(Utils.VerbosityOptions.normal);

    public readonly NuGetRestoreOptions RestoreOptions = new(forward: true);

    public ToolExecuteCommandDefinitionBase(string name)
        : base(name, CommandDefinitionStrings.ToolExecuteCommandDescription)
    {
        Arguments.Add(PackageIdentityArgument);
        Arguments.Add(CommandArgument);

        Options.Add(VersionOption);
        Options.Add(YesOption);
        Options.Add(RollForwardOption);
        Options.Add(PrereleaseOption);
        Options.Add(ConfigOption);
        Options.Add(SourceOption);
        Options.Add(AddSourceOption);
        Options.Add(VerbosityOption);

        RestoreOptions.AddTo(Options);
    }
}
