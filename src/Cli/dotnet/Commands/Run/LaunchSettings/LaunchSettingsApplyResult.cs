// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable warnings

namespace Microsoft.DotNet.Cli.Commands.Run.LaunchSettings;

public class LaunchSettingsApplyResult(bool success, string? failureReason, ProjectLaunchSettingsModel launchSettings = null)
{
    public bool Success { get; } = success;

    public string FailureReason { get; } = failureReason;

    public ProjectLaunchSettingsModel LaunchSettings { get; } = launchSettings;
}
