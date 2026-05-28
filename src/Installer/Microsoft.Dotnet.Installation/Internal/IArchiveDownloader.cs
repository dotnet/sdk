// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Interface for downloading .NET archives. Enables testing without network access.
/// </summary>
internal interface IArchiveDownloader : IDisposable
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
}
