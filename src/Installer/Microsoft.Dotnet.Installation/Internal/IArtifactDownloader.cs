// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Interface for downloading .NET archives and installers. Enables testing without network access.
/// </summary>
internal interface IArtifactDownloader
{
    /// <summary>
    /// Downloads and verifies the artifact, returning its path, expected hash, and whether
    /// unsigned-download policy applies. Appends the artifact's file extension to the base path.
    /// </summary>
    /// <param name="installRequest">The installation request containing component and install root info.</param>
    /// <param name="resolvedVersion">The resolved version to download.</param>
    /// <param name="destinationBasePath">The local base path (without extension) to save the downloaded file.</param>
    /// <param name="progress">Optional progress reporting.</param>
    VerifiedDownload DownloadVerifiedArtifact(
        DotnetInstallRequest installRequest,
        ReleaseVersion resolvedVersion,
        string destinationBasePath,
        IProgress<DownloadProgress>? progress = null);
}

/// <summary>A downloaded artifact and the facts its verification established.</summary>
/// <param name="Path">Full path of the downloaded file, including the resolved file extension.</param>
/// <param name="ExpectedHash">The SHA-512 hash the download was verified against.</param>
/// <param name="IsUnsigned">
/// True when the hash was obtained without authenticated release metadata, so unsigned-download policy applies.
/// This does not describe the file's Authenticode signature; installer signature verification is separate.
/// </param>
internal sealed record VerifiedDownload(string Path, string ExpectedHash, bool IsUnsigned);
