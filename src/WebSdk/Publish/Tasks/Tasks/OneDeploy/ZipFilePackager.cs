// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Compression;

namespace Microsoft.NET.Sdk.Publish.Tasks.OneDeploy;

/// <summary>
/// Packages file(s) to a .zip file.
/// </summary>
internal class ZipFilePackager : IFilePackager
{
    /// <inheritdoc/>
    public string Extension => ".zip";

    /// <inheritdoc/>
    public Task<bool> CreatePackageAsync(string sourcePath, string destinationPath, CancellationToken cancellation)
    {
        ZipFile.CreateFromDirectory(sourcePath, destinationPath);

        // no way to know if ZipFile succeeded; assume it always does
        return System.Threading.Tasks.Task.FromResult(true);
    }
}
