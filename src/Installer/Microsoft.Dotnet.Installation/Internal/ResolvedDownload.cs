// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Carries a pinned artifact, its checksum, and version/RID metadata. Unsigned metadata is not authentication.
/// </summary>
/// <remarks>
/// <see cref="IsUnsigned"/> marks downloads subject to unsigned-download policy: integrity is checked by hash,
/// but authenticity is not established by signed release metadata or Authenticode verification required before use.
/// It does not report the source URL or whether the file contains a signature.
/// </remarks>
internal sealed record ResolvedDownload(
    Uri DownloadUri,
    string ExpectedHash,
    string Rid,
    ReleaseVersion Version,
    bool IsUnsigned = false,
    bool IsDotnetup = false);