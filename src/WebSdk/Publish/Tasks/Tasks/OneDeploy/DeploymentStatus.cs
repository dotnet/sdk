// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.Publish.Tasks.OneDeploy;

/// <summary>
/// Deployment operation status codes
/// </summary>
public enum DeploymentStatus
{
    Unknown = -10,
    Cancelled = -1,
    Pending = 0,
    Building = 1,
    Deploying = 2,
    Failed = 3,
    Success = 4,
    Conflict = 5,
    PartialSuccess = 6
}

/// <summary>
/// Extension methods for <see cref="DeploymentStatus"/>.
/// </summary>
internal static class DeployStatusExtensions
{
    /// <summary>
    /// Whether this status represents a successful status.
    /// </summary>
    internal static bool IsSuccessfulStatus(this DeploymentStatus status)
    {
        return status == DeploymentStatus.Success
            || status == DeploymentStatus.PartialSuccess;
    }

    /// <summary>
    /// Whether this status represents a failed status.
    /// </summary>
    internal static bool IsFailedStatus(this DeploymentStatus status)
    {
        return status == DeploymentStatus.Failed
            || status == DeploymentStatus.Conflict
            || status == DeploymentStatus.Cancelled
            || status == DeploymentStatus.Unknown;
    }

    /// <summary>
    /// Whether this status represents the 'final' status for an on-going deployment operation.
    /// </summary>
    /// <returns>true if is is terminating; false, otherwise</returns>
    internal static bool IsTerminatingStatus(this DeploymentStatus status)
    {
        return status.IsSuccessfulStatus()
            || status.IsFailedStatus();
    }
}
