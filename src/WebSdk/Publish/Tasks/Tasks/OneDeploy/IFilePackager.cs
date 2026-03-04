// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.Publish.Tasks.OneDeploy;

/// <summary>
/// Takes the content off of a location and produces a package (e.g., a .zip file).
/// </summary>
internal interface IFilePackager
{
    /// <summary>
    /// Extension of produced package (including the '.'). E.g.: '.zip', '.tar', etc.
    /// </summary>
    string Extension { get; }

    /// <summary>
    /// Packages the content in the given sourcePath
    /// </summary>
    /// <param name="sourcePath">path to the content to package</param>
    /// <param name="destinationPath">path to the packaged content</param>
    /// <param name="cancellation">cancellation token</param>
    /// <returns>whether content was packaged or not</returns>
    Task<bool> CreatePackageAsync(string sourcePath, string destinationPath, CancellationToken cancellation);
}
