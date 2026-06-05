// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;

/// <summary>
/// The recommended setup the walkthrough plans to apply, resolved before the summary is shown.
/// This is display/decision state only and is resolved side-effect-free; the concrete install
/// requests are resolved separately once the user commits to installing. Choosing "customize"
/// discards this plan and re-resolves each value through the step-by-step prompts.
/// </summary>
/// <param name="InstallRoot">The install root the environment is configured against.</param>
/// <param name="PathPreference">The recommended path preference (mode).</param>
/// <param name="Migrations">The system installs eligible for migration under the recommended mode.</param>
/// <param name="ChannelDisplay">Display information for the SDK channel line.</param>
internal sealed record WalkthroughPlan(
    DotnetInstallRoot InstallRoot,
    PathPreference PathPreference,
    List<MigrationWorkflow.MigrationSelection> Migrations,
    DefaultChannelDisplay ChannelDisplay);
