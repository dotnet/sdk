// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Run.LaunchSettings;

public sealed class ExecutableLaunchSettingsModel : LaunchSettingsModel
{
    public const string WorkingDirectoryPropertyName = "workingDirectory";
    public const string ExecutablePathPropertyName = "executablePath";

    public required string ExecutablePath { get; init; }
    public string? WorkingDirectory { get; init; }
}
