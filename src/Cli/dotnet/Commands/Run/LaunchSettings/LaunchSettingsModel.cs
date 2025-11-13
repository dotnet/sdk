// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Run.LaunchSettings;

public abstract class LaunchSettingsModel
{
    public string? LaunchProfileName { get; set; }

    public string? CommandLineArgs { get; set; }

    public Dictionary<string, string> EnvironmentVariables { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public abstract LaunchProfileKind ProfileKind { get; }
}

public enum LaunchProfileKind
{
    Project,
    Executable
}
