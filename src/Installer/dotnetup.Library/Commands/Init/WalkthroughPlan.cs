// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;

/// <summary>
/// The recommended setup the walkthrough plans to apply, resolved before the summary is shown.
/// This is display/decision state only and is resolved side-effect-free; the concrete install
/// requests are resolved separately once the user commits to installing. Choosing "customize"
/// discards this plan and re-resolves each value through the step-by-step prompts.
/// </summary>
/// <param name="InstallRoot">The install root the environment is configured against.</param>
/// <param name="AccessMode">The recommended access mode.</param>
/// <param name="Migrations">The system installs eligible for migration under the recommended mode.</param>
/// <param name="ChannelDisplay">Display information for the SDK channel line.</param>
/// <param name="ShellProvider">The resolved shell provider used to recommend and describe terminal mode.</param>
/// <param name="InstallRootGlobalJsonPath">The global.json that supplied the install root, if any.</param>
internal sealed record WalkthroughPlan(
    DotnetInstallRoot InstallRoot,
    DotnetAccessMode AccessMode,
    List<MigrationWorkflow.MigrationSelection> Migrations,
    DefaultChannelDisplay ChannelDisplay,
    IEnvShellProvider? ShellProvider,
    string? InstallRootGlobalJsonPath);
