// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Builds URLs for downloading .NET archives and their SHA-512 checksum files
/// from the public dotnet blob feeds. Used when a version is not present in the
/// release manifest (e.g. daily/preview builds).
/// </summary>
internal static class BlobFeedUrlBuilder
{
    /// <summary>
    /// Primary blob feed where stable/serviced builds are hosted. Archives and
    /// their .sha512 companions are co-located at the same path.
    /// </summary>
    public const string PrimaryFeedBaseUrl = "https://builds.dotnet.microsoft.com/dotnet";

    /// <summary>
    /// Fallback blob feed where daily/preview builds are hosted. Archives and
    /// checksums live on different path roots ("public" vs "public-checksums").
    /// </summary>
    public const string FallbackFeedArchiveBaseUrl = "https://ci.dot.net/public";
    public const string FallbackFeedChecksumBaseUrl = "https://ci.dot.net/public-checksums";

    /// <summary>
    /// Describes a candidate blob feed location (archive URL + matching .sha512 URL).
    /// </summary>
    public readonly record struct BlobFeedLocation(string ArchiveUrl, string ChecksumUrl);

    /// <summary>
    /// Returns the primary then fallback feed locations to try, in order, for the
    /// given component/version/RID/extension.
    /// </summary>
    public static IEnumerable<BlobFeedLocation> GetFeedLocations(
        InstallComponent component,
        ReleaseVersion version,
        string rid,
        string extension)
    {
        string componentDir = GetComponentDirectory(component);
        string fileName = GetArchiveFileName(component, version, rid, extension);
        string versionString = version.ToString();

        // Primary feed: archive + checksum co-located.
        string primaryDir = $"{PrimaryFeedBaseUrl}/{componentDir}/{versionString}";
        yield return new BlobFeedLocation(
            ArchiveUrl: $"{primaryDir}/{fileName}",
            ChecksumUrl: $"{primaryDir}/{fileName}.sha512");

        // Fallback feed: archive on /public, checksum on /public-checksums.
        yield return new BlobFeedLocation(
            ArchiveUrl: $"{FallbackFeedArchiveBaseUrl}/{componentDir}/{versionString}/{fileName}",
            ChecksumUrl: $"{FallbackFeedChecksumBaseUrl}/{componentDir}/{versionString}/{fileName}.sha512");
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
