// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Per-batch context threaded through the install pipeline so that the orchestrator,
/// downloader/extractor, and progress tracker share the same reporter and column-alignment
/// metadata without each layer taking them as separate parameters.
/// </summary>
/// <param name="Reporter">
/// Shared progress reporter for all installs in the batch. Lifetime is owned by the caller
/// that constructed the context.
/// </param>
/// <param name="VersionDisplayWidth">
/// Minimum width for the version column in progress rows — typically the length of the
/// longest resolved version in the batch — so mixed short/long version rows align vertically.
/// </param>
internal sealed record InstallBatchContext(IProgressReporter Reporter, int VersionDisplayWidth)
{
    private int _cancellationRequested;

    /// <summary>
    /// True once the user asked to cancel the batch while an install could not be interrupted. Installs that
    /// have not started committing are skipped; the running one finishes and reports its real result.
    /// </summary>
    public bool IsCancellationRequested => Volatile.Read(ref _cancellationRequested) != 0;

    /// <summary>Records that the user asked to cancel the batch. Safe to call from any thread.</summary>
    public void RequestCancellation() => Interlocked.Exchange(ref _cancellationRequested, 1);
}
