// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;

/// <summary>
/// Runs the interactive initialization flow that installs the .NET SDK with defaults
/// and records the user's path replacement preference to <c>dotnetup.config.json</c>.
/// </summary>
internal class InitCommand(ParseResult result) : InstallCommand(result)
{
    protected override string GetCommandName() => "init";

    protected override int ExecuteCore()
    {
        var workflows = new InitWorkflows(DotnetEnvironment, ChannelVersionResolver);
        workflows.InitWalkthrough(this);
        return 0;
    }
}
