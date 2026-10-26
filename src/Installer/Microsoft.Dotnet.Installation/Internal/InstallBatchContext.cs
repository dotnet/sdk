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
internal sealed record InstallBatchContext(IProgressReporter Reporter, int VersionDisplayWidth);
