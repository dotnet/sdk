using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Carries a pinned artifact and its integrity metadata; BuildId is required for dotnetup self-update.
/// </summary>
internal sealed record ResolvedDownload(
    Uri DownloadUri,
    string ExpectedHash,
    string Rid,
    ReleaseVersion Version,
    string? BuildId = null,
    bool IsUnsigned = false)
{
    public bool IsDotnetup => BuildId is not null || DownloadUri.AbsolutePath.StartsWith("/public/dotnetup/", StringComparison.Ordinal);
}