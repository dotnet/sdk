// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.DotNet.ProjectTools;

public abstract class LaunchProfile
{
    public string? LaunchProfileName { get; init; }

    public bool DotNetRunMessages { get; init; }

    public string? CommandLineArgs { get; init; }

    public required ImmutableDictionary<string, string> EnvironmentVariables { get; init; }
}
