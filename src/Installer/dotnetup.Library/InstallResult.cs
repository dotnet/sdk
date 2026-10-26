
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.Dotnet.Installation;

/// <summary>
/// Result of an installation operation.
/// </summary>
/// <param name="Install">The DotnetInstall for the completed installation.</param>
/// <param name="WasAlreadyInstalled">True if the SDK was already installed and no work was done.</param>
internal record InstallResult(DotnetInstall Install, bool WasAlreadyInstalled);

/// <summary>
/// Records a failed installation within a batch so that other installs can continue.
/// </summary>
/// <param name="Request">The request that failed.</param>
/// <param name="Exception">The exception describing the failure.</param>
internal record InstallFailure(ResolvedInstallRequest Request, DotnetInstallException Exception);

/// <summary>
/// Aggregated outcome of a batch install. Contains both successful and failed results
/// so callers can display partial-success summaries.
/// </summary>
/// <param name="Successes">Installs that completed or were already present.</param>
/// <param name="Failures">Installs that failed with a recoverable error.</param>
internal record InstallBatchResult(IReadOnlyList<InstallResult> Successes, IReadOnlyList<InstallFailure> Failures)
{
    /// <summary>True when every install in the batch succeeded.</summary>
    public bool AllSucceeded => Failures.Count == 0;
}
