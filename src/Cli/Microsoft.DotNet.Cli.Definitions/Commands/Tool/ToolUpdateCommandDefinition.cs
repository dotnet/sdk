// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Tool.Update;

internal sealed class ToolUpdateCommandDefinition : ToolUpdateInstallCommandDefinition
{
    public readonly Argument<PackageIdentityWithRange?> PackageIdentityArgument = CommonArguments.CreateOptionalPackageIdentityArgument("dotnetsay", "2.1.7");
    public readonly Option<bool> UpdateAllOption = ToolAppliedOption.CreateUpdateAllOption();

    public ToolUpdateCommandDefinition()
        : base("update", CommandDefinitionStrings.ToolUpdateCommandDescription)
    {
        Arguments.Add(PackageIdentityArgument);
        Options.Add(UpdateAllOption);
    }
}
