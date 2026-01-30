// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

/// <summary>
/// Strongly-typed event data for install operations.
/// </summary>
/// <param name="Component">The component being installed (e.g., "sdk", "runtime").</param>
/// <param name="Version">The version being installed.</param>
/// <param name="PreviousVersion">The previous version if this is an update.</param>
/// <param name="WasUpdate">Whether this was an update operation.</param>
/// <param name="InstallRoot">The installation root path (will be hashed).</param>
/// <param name="DownloadDuration">Time spent downloading.</param>
/// <param name="ExtractionDuration">Time spent extracting.</param>
/// <param name="ArchiveSizeBytes">Size of the downloaded archive in bytes.</param>
public record InstallEventData(
    string Component,
    string Version,
    string? PreviousVersion,
    bool WasUpdate,
    string InstallRoot,
    TimeSpan DownloadDuration,
    TimeSpan ExtractionDuration,
    long ArchiveSizeBytes
);

/// <summary>
/// Strongly-typed event data for update operations.
/// </summary>
/// <param name="Component">The component being updated.</param>
/// <param name="FromVersion">The version updating from.</param>
/// <param name="ToVersion">The version updating to.</param>
/// <param name="UpdateChannel">The update channel (e.g., "lts", "sts").</param>
/// <param name="WasAutomatic">Whether this was an automatic update.</param>
public record UpdateEventData(
    string Component,
    string FromVersion,
    string ToVersion,
    string UpdateChannel,
    bool WasAutomatic
);

/// <summary>
/// Strongly-typed event data for command completion.
/// </summary>
/// <param name="Command">The command that was executed.</param>
/// <param name="ExitCode">The exit code of the command.</param>
/// <param name="Duration">The duration of the command.</param>
/// <param name="Success">Whether the command succeeded.</param>
public record CommandCompletedEventData(
    string Command,
    int ExitCode,
    TimeSpan Duration,
    bool Success
);
