// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Dotnetup.Release;

public sealed record CandidateManifest(int SchemaVersion, string BuildId, string Commit, string Variant, string Version, string Rid, bool Placeholder);
public sealed record AkaOwnership(string Owners, string Actor, string GroupOwner);
public sealed record BlobUpload(string Method, string Source, string Destination, string Sha512, string ContentType, string CacheControl, string ContentDisposition);
public sealed record AkaPayload(string TargetUrl, bool IsAllowParam, string Description, string GroupOwner, string LastModifiedBy, string Owners, string Category);
public sealed record AkaCreatePayload(string ShortUrl, string TargetUrl, string CreatedBy, string LastModifiedBy, bool IsVanity, bool IsAllowParam, string Description, string GroupOwner, string Owners, string Category);
public sealed record AkaUpdate(string ReadMethod, string ReadUri, string IfExistsMethod, AkaPayload UpdateBody, string IfNotFoundMethod, string CreateUri, AkaCreatePayload CreateBody);
public sealed record ReleasePlan(bool DryRun, string BuildId, string Commit, string Quality, string Version, bool Placeholder, string[] Preconditions, BlobUpload[] Uploads, AkaUpdate[] Links);

public static partial class ReleasePlanner
{
    private static readonly string[] s_rids = ["linux-arm64", "linux-musl-arm64", "linux-musl-x64", "linux-x64", "osx-arm64", "osx-x64", "win-arm64", "win-x64"];
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<ReleasePlan> CreateAsync(string artifactDirectory, string quality, string buildId, string commit, AkaOwnership ownership)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(buildId);
        ArgumentException.ThrowIfNullOrWhiteSpace(commit);
        if (quality is not ("preview" or "ga"))
        {
            throw new ArgumentException("Only preview and ga can be promoted.", nameof(quality));
        }

        List<(string Path, CandidateManifest Manifest)> candidates = [];
        EnumerationOptions enumerationOptions = new() { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
        foreach (string manifestPath in Directory.EnumerateFiles(artifactDirectory, "release.json", enumerationOptions).Order(StringComparer.Ordinal))
        {
            CandidateManifest manifest = JsonSerializer.Deserialize<CandidateManifest>(await File.ReadAllTextAsync(manifestPath).ConfigureAwait(false), s_jsonOptions)
                ?? throw new InvalidDataException($"Empty candidate manifest: {manifestPath}");
            if (manifest.Variant != quality)
            {
                continue;
            }

            ValidateManifest(manifest, manifestPath, artifactDirectory, buildId, commit, quality);
            candidates.Add((manifestPath, manifest));
        }

        string[] actualRids = candidates.Select(candidate => candidate.Manifest.Rid).Order(StringComparer.Ordinal).ToArray();
        if (!actualRids.SequenceEqual(s_rids, StringComparer.Ordinal))
        {
            throw new InvalidDataException($"Expected exactly one candidate per RID: {string.Join(", ", s_rids)}. Found: {string.Join(", ", actualRids)}.");
        }

        string version = candidates[0].Manifest.Version;
        if (candidates.Any(candidate => candidate.Manifest.Version != version))
        {
            throw new InvalidDataException("Candidates do not share one version.");
        }

        SortedDictionary<string, (string Path, string Hash)> assets = new(StringComparer.Ordinal);
        foreach ((string manifestPath, CandidateManifest manifest) in candidates)
        {
            string directory = Path.GetDirectoryName(manifestPath)!;
            List<string> expectedFiles = [$"dotnetup-{manifest.Rid}{(manifest.Rid.StartsWith("win-", StringComparison.Ordinal) ? ".exe" : "")}"];
            if (manifest.Rid == "win-x64")
            {
                expectedFiles.AddRange(["get-dotnetup.ps1", "get-dotnetup.sh"]);
            }

            foreach (string fileName in expectedFiles)
            {
                string path = Path.Combine(directory, fileName);
                string hash = await GetHashAsync(path).ConfigureAwait(false);
                string checksumHash = await GetHashAsync(path + ".sha512").ConfigureAwait(false);
                string[] checksumParts = (await File.ReadAllTextAsync(path + ".sha512").ConfigureAwait(false)).Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (checksumParts.Length == 0 || !ChecksumPattern().IsMatch(checksumParts[0]) || !hash.Equals(checksumParts[0], StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"SHA-512 mismatch: {path}");
                }

                assets.Add(fileName, (path, hash));
                assets.Add(fileName + ".sha512", (path + ".sha512", checksumHash));
            }

            string[] allowedFiles = [.. expectedFiles, .. expectedFiles.Select(name => name + ".sha512"), "release.json"];
            if (Directory.EnumerateFileSystemEntries(directory).Any(path => !allowedFiles.Contains(Path.GetFileName(path), StringComparer.Ordinal)))
            {
                throw new InvalidDataException($"Unexpected payload file in {directory}.");
            }
        }

        List<BlobUpload> uploads = [];
        List<AkaUpdate> links = [];
        foreach (KeyValuePair<string, (string Path, string Hash)> asset in assets)
        {
            string fileName = asset.Key;
            string blobPath = $"dotnetup/{version}/{fileName}";
            bool isChecksum = fileName.EndsWith(".sha512", StringComparison.Ordinal);
            int ttl = isChecksum || fileName.EndsWith(".ps1", StringComparison.Ordinal) || fileName.EndsWith(".sh", StringComparison.Ordinal) ? 900
                : fileName.EndsWith(".exe", StringComparison.Ordinal) ? 864000 : 86400;
            foreach (string account in new[] { "dotnetorigin", "dotnetcli" })
            {
                uploads.Add(new BlobUpload("PUT", asset.Value.Path, $"https://{account}.blob.core.windows.net/dotnet/{blobPath}", asset.Value.Hash,
                    isChecksum ? "text/plain" : "application/octet-stream", FormattableString.Invariant($"public, max-age={ttl}"), "attachment"));
            }

            // The product calls the GA channel "stable".
            string shortUrl = $"dotnet/dotnetup/{(quality == "ga" ? "stable" : quality)}/{fileName}";
            string api = "https://redirectionapi-ame.trafficmanager.net/api/aka/1/ncd/";
            string targetUrl = $"https://builds.dotnet.microsoft.com/dotnet/{blobPath}";
            string description = $"dotnetup {quality}: {fileName}";
            links.Add(new AkaUpdate("GET", api + shortUrl, "PUT",
                new AkaPayload(targetUrl, false, description, ownership.GroupOwner, ownership.Actor, ownership.Owners, "dotnet"),
                "POST", api, new AkaCreatePayload(shortUrl, targetUrl, ownership.Actor, ownership.Actor, true, false, description, ownership.GroupOwner, ownership.Owners, "dotnet")));
        }

        return new ReleasePlan(true, buildId, commit, quality, version, quality == "ga",
            [
                "DRY RUN ONLY: no storage or aka.ms write APIs are called.",
                "Configure and approve aka.ms ownership and service identities before enabling any future live implementation.",
                "Authenticate aka.ms reads/writes with scope https://msazurecloud.onmicrosoft.com/RedirectionMgmtApi-Prod/.default.",
                "Upload all versioned blobs to BOTH accounts before changing ANY links.",
                "Create blobs with If-None-Match: *. Reuse an existing blob only after verifying identical SHA-512; otherwise fail without overwriting.",
                "Verify all uploaded bytes and headers before updating links. Link changes are not transactional.",
                "For aka.ms GET, use PUT only on 200 and POST only on 404. Any other status must fail.",
                "GA artifacts are placeholders, not approved releases. A live implementation must explicitly reject them."
            ], uploads.ToArray(), links.ToArray());
    }

    private static void ValidateManifest(CandidateManifest manifest, string path, string root, string buildId, string commit, string quality)
    {
        string[] parts = Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar);
        if (parts.Length != 4 || !parts[0].StartsWith("Dotnetup_", StringComparison.Ordinal) || parts[1] != quality || parts[2] != manifest.Version
            || manifest.SchemaVersion != 1 || manifest.BuildId != buildId || manifest.Commit != commit
            || !s_rids.Contains(manifest.Rid, StringComparer.Ordinal) || manifest.Placeholder != (quality == "ga")
            || !(quality == "ga" ? StableVersionPattern() : PreviewVersionPattern()).IsMatch(manifest.Version))
        {
            throw new InvalidDataException($"Invalid candidate identity, layout, or provenance: {path}");
        }
    }

    private static async Task<string> GetHashAsync(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 || new FileInfo(path).Length == 0)
        {
            throw new InvalidDataException($"Empty or linked release payload: {path}");
        }

        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA512.HashDataAsync(stream).ConfigureAwait(false));
    }

    [GeneratedRegex(@"^[0-9]+\.[0-9]+\.[0-9]+-preview\.[0-9]+\.[0-9]+\.[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex PreviewVersionPattern();

    [GeneratedRegex(@"^[0-9]+\.[0-9]+\.[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex StableVersionPattern();

    [GeneratedRegex(@"^[0-9a-fA-F]{128}$", RegexOptions.CultureInvariant)]
    private static partial Regex ChecksumPattern();
}
