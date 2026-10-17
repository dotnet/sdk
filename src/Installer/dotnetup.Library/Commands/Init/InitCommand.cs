// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;

/// <summary>
/// Runs the interactive initialization flow that installs the .NET SDK with defaults
/// and records the user's access mode to <c>dotnetup.config.json</c>.
/// </summary>
internal class InitCommand(ParseResult result, DotnetupPolicy? policy = null) : InstallCommand(result, "init", policy)
{
    private readonly DotnetAccessMode? _requestedAccessMode = result.GetValue(InitCommandParser.ModeOption);

    /// <inheritdoc/>
    internal override AdminModeSupport GetAdminModeSupport(Command command) => AdminModeSupport.AnySymbol;

    protected override void ExecuteCore()
    {
        var workflows = new InitWorkflows(DotnetEnvironment, new AdminModeSwitcher(Policy));
        workflows.RunInit(this, _requestedAccessMode);
    }
}
