// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Tool.Install;

internal sealed class ToolInstallCommandDefinition : ToolUpdateInstallCommandDefinition
{
    public readonly Argument<PackageIdentityWithRange> PackageIdentityArgument = CommonArguments.CreateRequiredPackageIdentityArgument("dotnetsay", "2.1.7");

    public readonly Option<bool> CreateManifestIfNeededOption = new("--create-manifest-if-needed")
    {
        Description = CommandDefinitionStrings.CreateManifestIfNeededOptionDescription,
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = _ => true,
    };

    // Don't use the common options version as we don't want this to be a forwarded option
    public readonly Option<string> ArchitectureOption = new("--arch", "-a")
    {
        Description = CommandDefinitionStrings.ArchitectureOptionDescription
    };

    public readonly Option<bool> RollForwardOption = ToolAppliedOption.CreateRollForwardOption();

    public ToolInstallCommandDefinition()
        : base("install", CommandDefinitionStrings.ToolInstallCommandDescription)
    {
        Arguments.Add(PackageIdentityArgument);

        Options.Add(ArchitectureOption);
        Options.Add(CreateManifestIfNeededOption);
        Options.Add(RollForwardOption);
    }
}
