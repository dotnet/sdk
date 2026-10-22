// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// A downloadable unit of installation. <see cref="Prepare"/> acquires the artifact (downloads may run
/// concurrently) and <see cref="Commit"/> lays it down on the machine (commits run sequentially).
/// </summary>
internal interface IInstallArtifact : IDisposable
{
    /// <summary>Downloads and verifies the artifact without modifying the installation.</summary>
    void Prepare();

    /// <summary>Installs the prepared artifact. <see cref="Prepare"/> must have completed first.</summary>
    void Commit();

    /// <summary>
    /// Whether the installed payload should be validated after <see cref="Commit"/> returns.
    /// </summary>
    bool RequiresPostInstallValidation { get; }

    /// <summary>The subcomponents laid down by the last <see cref="Commit"/> call, for manifest tracking.</summary>
    IReadOnlyList<string> ExtractedSubcomponents { get; }
}
