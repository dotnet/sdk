// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Runtime.Uninstall;

internal class RuntimeUninstallCommand(ParseResult result) : CommandBase(result)
{
    private readonly string _componentSpec = result.GetValue(RuntimeUninstallCommandParser.ComponentSpecArgument)!;
    private readonly InstallSource _sourceFilter = result.GetValue(CommonOptions.SourceOption);
    private readonly string? _manifestPath = result.GetValue(CommonOptions.ManifestPathOption);
    private readonly string? _installPath = result.GetValue(CommonOptions.InstallPathOption);

    protected override string GetCommandName() => "runtime/uninstall";

    protected override int ExecuteCore()
    {
        // Parse the component spec to determine runtime type and version/channel
        var (component, versionOrChannel) = Install.RuntimeInstallCommand.ParseComponentSpec(_componentSpec);

        if (string.IsNullOrEmpty(versionOrChannel))
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.InvalidChannel,
                "A version or channel must be specified for uninstall. " +
                "Examples: dotnetup runtime uninstall 9.0, dotnetup runtime uninstall aspnetcore@10.0");
        }

        return UninstallWorkflow.Execute(
            _manifestPath,
            _installPath,
            versionOrChannel,
            _sourceFilter,
            component);
    }
}
