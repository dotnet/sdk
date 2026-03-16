// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Tool.Uninstall;

internal sealed class ToolUninstallCommandDefinition : Command
{
    public readonly Argument<string> PackageIdArgument = new("packageId")
    {
        HelpName = "PACKAGE_ID",
        Description = CliStrings.PackageReference,
        Arity = ArgumentArity.ExactlyOne
    };

    public readonly ToolLocationOptions LocationOptions = new(
        globalOptionDescription: CliCommandStrings.ToolUninstallGlobalOptionDescription,
        localOptionDescription: CliCommandStrings.ToolUninstallLocalOptionDescription,
        toolPathOptionDescription: CliCommandStrings.ToolUninstallToolPathOptionDescription);

    public readonly Option<string> ToolManifestOption = ToolAppliedOption.CreateToolManifestOption(CliCommandStrings.ToolUninstallManifestPathOptionDescription);

    public ToolUninstallCommandDefinition()
        : base("uninstall", CliCommandStrings.ToolUninstallCommandDescription)
    {
        Arguments.Add(PackageIdArgument);
        LocationOptions.AddTo(Options);
        Options.Add(ToolManifestOption);
    }
}
