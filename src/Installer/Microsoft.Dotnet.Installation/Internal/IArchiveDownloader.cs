// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Interface for downloading .NET archives. Enables testing without network access.
/// </summary>
internal interface IArchiveDownloader
{
    /// <summary>
    /// Downloads the archive for the specified installation request and verifies its hash.
    /// The implementation resolves the appropriate archive format and appends the file extension
    /// to the provided base path.
    /// </summary>
    /// <param name="installRequest">The installation request containing component and install root info.</param>
    /// <param name="resolvedVersion">The resolved version to download.</param>
    /// <param name="destinationBasePath">The local base path (without extension) to save the downloaded file.</param>
    /// <param name="progress">Optional progress reporting.</param>
    /// <returns>The full path of the downloaded archive, including the resolved file extension.</returns>
    string DownloadArchiveWithVerification(
        DotnetInstallRequest installRequest,
        ReleaseVersion resolvedVersion,
        string destinationBasePath,
        IProgress<DownloadProgress>? progress = null);

    /// <summary>
    /// Downloads and verifies the artifact like <see cref="DownloadArchiveWithVerification"/>, and also returns the
    /// expected hash the download was verified against and whether unsigned-download policy applies.
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
