// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Tool.Execute;

namespace Microsoft.DotNet.Cli.Commands.Dnx;

internal sealed class DnxCommandDefinition : ToolExecuteCommandDefinitionBase
{
    public DnxCommandDefinition()
        : base("dnx", CommandDefinitionStrings.DnxPackageOrFileArgumentName)
    {
        Description = CommandDefinitionStrings.DnxCommandDescription;
        PackageIdentityArgument.Description = CommandDefinitionStrings.DnxPackageOrFileArgumentDescription;
        PackageIdentityArgument.HelpName = CommandDefinitionStrings.DnxPackageOrFileArgumentName;
        CommandArgument.Description = CommandDefinitionStrings.DnxArgumentsDescription;
        Hidden = true;
    }
}
