// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.DotNet.Cli.Commands.Run.LaunchSettings;

public abstract class LaunchSettingsModel
{
    /// <summary>
    /// Path to the .json file containing the launch settings.
    /// Used to resolve relative paths in the launch settings.
    /// </summary>
    public required string LaunchSettingsPath { get; init; }

    public string? LaunchProfileName { get; init; }

    public bool DotNetRunMessages { get; init; }

    public string? CommandLineArgs { get; init; }

    public required ImmutableDictionary<string, string> EnvironmentVariables { get; init; }
}
