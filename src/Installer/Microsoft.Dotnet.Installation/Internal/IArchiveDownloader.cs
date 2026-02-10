// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Interface for downloading .NET archives. Enables testing without network access.
/// </summary>
internal interface IArchiveDownloader : IDisposable
{
    /// <summary>
    /// Downloads the archive for the specified installation request and verifies its hash.
    /// </summary>
    /// <param name="installRequest">The installation request containing component and install root info.</param>
    /// <param name="resolvedVersion">The resolved version to download.</param>
    /// <param name="destinationPath">The local path to save the downloaded file.</param>
    /// <param name="progress">Optional progress reporting.</param>
    void DownloadArchiveWithVerification(
        DotnetInstallRequest installRequest,
        ReleaseVersion resolvedVersion,
        string destinationPath,
        IProgress<DownloadProgress>? progress = null);
}
