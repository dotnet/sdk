// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Uninstall;

internal class SdkUninstallCommand(ParseResult result) : UninstallCommand(result, "sdk/uninstall")
{
    private readonly string _versionOrChannel = result.GetValue(SdkUninstallCommandParser.ChannelArgument)!;

    protected override void ExecuteCore()
    {
        ExecuteUninstall(_versionOrChannel, InstallComponent.SDK);
    }
}
