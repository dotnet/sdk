// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

/// <summary>
/// Provides the shared option handling and workflow invocation for SDK and runtime uninstall commands.
/// Component-specific commands remain responsible for parsing their target argument.
/// </summary>
internal abstract class UninstallCommand : CommandBase
{
    private readonly InstallSource _sourceFilter;
    private readonly string? _manifestPath;
    private readonly string? _installPath;
    private readonly bool _interactive;

    protected UninstallCommand(ParseResult parseResult, string commandName)
        : base(parseResult, commandName)
    {
        _sourceFilter = parseResult.GetValue(CommonOptions.SourceOption);
        _manifestPath = parseResult.GetValue(CommonOptions.ManifestPathOption);
        _installPath = parseResult.GetValue(CommonOptions.InstallPathOption);
        _interactive = parseResult.GetValue(CommonOptions.InteractiveOption)
            && !parseResult.GetValue(CommonOptions.NonInteractiveOption);
    }

    protected void ExecuteUninstall(string versionOrChannel, InstallComponent component)
    {
        UninstallWorkflow.Execute(
            _manifestPath,
            _installPath,
            versionOrChannel,
            _sourceFilter,
            component,
            _interactive);
    }
}
