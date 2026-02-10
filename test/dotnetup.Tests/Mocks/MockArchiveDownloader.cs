// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Mocks;

/// <summary>
/// Mock implementation of IArchiveDownloader for testing without network access.
/// </summary>
internal class MockArchiveDownloader : IArchiveDownloader
{
    /// <summary>
    /// Records all calls to DownloadArchiveWithVerification for verification.
    /// </summary>
    public List<DownloadCall> DownloadCalls { get; } = new();

    /// <summary>
    /// If set, DownloadArchiveWithVerification will throw this exception.
    /// </summary>
    public Exception? ExceptionToThrow { get; set; }

    /// <summary>
    /// If true, creates a fake archive file at the destination path.
    /// </summary>
    public bool CreateFakeArchive { get; set; } = true;

    /// <summary>
    /// Content to write to the fake archive file.
    /// </summary>
    public byte[] FakeArchiveContent { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Records a download call.
    /// </summary>
    public record DownloadCall(
        DotnetInstallRequest Request,
        ReleaseVersion Version,
        string DestinationPath);

    public void DownloadArchiveWithVerification(
        DotnetInstallRequest installRequest,
        ReleaseVersion resolvedVersion,
        string destinationPath,
        IProgress<DownloadProgress>? progress = null)
    {
        DownloadCalls.Add(new DownloadCall(installRequest, resolvedVersion, destinationPath));

        if (ExceptionToThrow != null)
        {
            throw ExceptionToThrow;
        }

        if (CreateFakeArchive)
        {
            // Ensure directory exists
            var dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Create a fake archive file
            File.WriteAllBytes(destinationPath, FakeArchiveContent);
        }

        // Report progress completion
        progress?.Report(new DownloadProgress(100, 100));
    }

    public void Dispose()
    {
        // Nothing to dispose
    }
}
