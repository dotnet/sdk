// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Carries a pinned artifact, its checksum, and version/RID metadata. Unsigned metadata is not authentication.
/// </summary>
internal sealed record ResolvedDownload(
    Uri DownloadUri,
    string ExpectedHash,
    string Rid,
    ReleaseVersion Version,
    bool IsUnsigned = false)
{
    public bool IsDotnetup => DownloadUri.AbsolutePath.StartsWith("/public/dotnetup/", StringComparison.Ordinal);
}