// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Builds URLs for downloading .NET archives and their SHA-512 checksum files
/// from the public dotnet blob feed at ci.dot.net. Used when a version is not
/// present in the release manifest (e.g. daily/preview builds). Versions that
/// are served by builds.dotnet.microsoft.com are also listed in the release
/// manifest, so they don't reach this fallback path.
/// </summary>
internal static class BlobFeedUrlBuilder
{
    /// <summary>
    /// Blob feed root for archive downloads (daily/preview builds).
    /// </summary>
    public const string ArchiveBaseUrl = "https://ci.dot.net/public";

    /// <summary>
    /// Blob feed root for SHA-512 checksum files. Hosted on a separate path from
    /// the archives so that checksums aren't tampered alongside the artifacts.
    /// </summary>
    public const string ChecksumBaseUrl = "https://ci.dot.net/public-checksums";

    /// <summary>
    /// A candidate blob feed location (archive URL + matching .sha512 URL).
    /// </summary>
    public readonly record struct BlobFeedLocation(string ArchiveUrl, string ChecksumUrl);

    /// <summary>
    /// Returns the blob feed location for the given component/version/RID/extension.
    /// </summary>
    public static BlobFeedLocation GetFeedLocation(
        InstallComponent component,
        ReleaseVersion version,
        string rid,
        string extension)
    {
        string componentDir = GetComponentDirectory(component);
        string fileName = GetArchiveFileName(component, version, rid, extension);
        string versionString = version.ToString();

        return new BlobFeedLocation(
            ArchiveUrl: $"{ArchiveBaseUrl}/{componentDir}/{versionString}/{fileName}",
            ChecksumUrl: $"{ChecksumBaseUrl}/{componentDir}/{versionString}/{fileName}.sha512");
    }

    /// <summary>
    /// Component → directory segment used in blob feed URLs.
    /// </summary>
    public static string GetComponentDirectory(InstallComponent component) => component switch
    {
        InstallComponent.SDK => "Sdk",
        InstallComponent.Runtime => "Runtime",
        InstallComponent.ASPNETCore => "aspnetcore/Runtime",
        InstallComponent.WindowsDesktop => "WindowsDesktop",
        _ => throw new ArgumentOutOfRangeException(nameof(component), component, "Unsupported install component"),
    };

    /// <summary>
    /// Archive filename, e.g. "dotnet-sdk-10.0.100-preview.4.25216.37-win-x64.zip".
    /// </summary>
    public static string GetArchiveFileName(InstallComponent component, ReleaseVersion version, string rid, string extension)
    {
        string prefix = component switch
        {
            InstallComponent.SDK => "dotnet-sdk",
            InstallComponent.Runtime => "dotnet-runtime",
            InstallComponent.ASPNETCore => "aspnetcore-runtime",
            InstallComponent.WindowsDesktop => "windowsdesktop-runtime",
            _ => throw new ArgumentOutOfRangeException(nameof(component), component, "Unsupported install component"),
        };

        return $"{prefix}-{version}-{rid}{extension}";
    }

    /// <summary>
    /// Parses the contents of a .sha512 file. Hash files commonly contain just
    /// the hex hash, optionally followed by whitespace and a filename
    /// (e.g. coreutils sha512sum format). Returns the lowercase hash.
    /// </summary>
    public static string ParseHashFile(string contents)
    {
        if (string.IsNullOrWhiteSpace(contents))
        {
            throw new FormatException("SHA-512 hash file is empty.");
        }

        // First whitespace-delimited token is the hex hash.
        var token = contents.AsSpan().Trim();
        int firstWhitespace = -1;
        for (int i = 0; i < token.Length; i++)
        {
            if (char.IsWhiteSpace(token[i]))
            {
                firstWhitespace = i;
                break;
            }
        }

        var hash = firstWhitespace < 0 ? token : token.Slice(0, firstWhitespace);

        // SHA-512 hex is 128 hex chars.
        if (hash.Length != 128)
        {
            throw new FormatException($"SHA-512 hash file does not contain a 128-character hex hash (got {hash.Length} chars).");
        }

        for (int i = 0; i < hash.Length; i++)
        {
            char c = hash[i];
            bool isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            if (!isHex)
            {
                throw new FormatException("SHA-512 hash file contains non-hex characters.");
            }
        }

        return hash.ToString().ToLowerInvariant();
    }
}
