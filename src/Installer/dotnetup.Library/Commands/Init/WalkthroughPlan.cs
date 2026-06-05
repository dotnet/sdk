// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;

/// <summary>
/// The recommended setup the walkthrough plans to apply, resolved before the summary is shown.
/// The summary renders this plan and the "proceed with defaults" branch reuses the exact same
/// values, so the displayed plan and the applied setup can never diverge. Choosing "customize"
/// discards this plan and re-resolves each value through the step-by-step prompts.
/// </summary>
/// <param name="Requests">The recommended install requests (resolved SDK channel).</param>
/// <param name="InstallRoot">The install root the environment is configured against.</param>
/// <param name="PathPreference">The recommended path preference (mode).</param>
/// <param name="Migrations">The system installs eligible for migration under the recommended mode.</param>
/// <param name="ChannelDisplay">Display information for the SDK channel line.</param>
internal sealed record WalkthroughPlan(
    List<ResolvedInstallRequest> Requests,
    DotnetInstallRoot InstallRoot,
    PathPreference PathPreference,
    List<MigrationWorkflow.MigrationSelection> Migrations,
    DefaultChannelDisplay ChannelDisplay);
