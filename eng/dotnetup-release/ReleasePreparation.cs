// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Dotnetup.Release;

internal static partial class ReleasePreparation
{
    private static readonly string[] s_rids = ["linux-arm64", "linux-musl-arm64", "linux-musl-x64", "linux-x64", "osx-arm64", "osx-x64", "win-arm64", "win-x64"];

    internal static async Task<ReleaseRecord> CreateAsync(string source, string variant, int buildId, string commit, JsonElement build)
    {
        if (variant is not ("preview" or "daily"))
        {
            throw new ArgumentException("Only preview and daily candidates can be released.", nameof(variant));
        }

        // Manual resource selection can override pipeline filters, so verify the actual source run first.
        ValidateBuild(build, buildId, commit);
        List<SourceCandidate> candidates = [];
        List<ReleaseAsset> files = [];
        HashSet<string> rids = new(StringComparer.Ordinal);
        string? version = null;
        // Read only the selected variant from RID candidate artifacts; never relabel another variant's binaries.
        foreach (string artifact in Directory.EnumerateDirectories(source, "Build_Dotnetup_*").Order(StringComparer.Ordinal))
        {
            string variantDirectory = Path.Combine(artifact, variant);
            if (!Directory.Exists(variantDirectory))
            {
                continue;
            }

            foreach (string directory in Directory.EnumerateDirectories(variantDirectory).Order(StringComparer.Ordinal))
            {
                if ((File.GetAttributes(artifact) & FileAttributes.ReparsePoint) != 0 ||
                    (File.GetAttributes(variantDirectory) & FileAttributes.ReparsePoint) != 0 ||
                    (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException($"Linked candidate directory: {directory}");
                }

                // Every manifest must identify the selected build and a unique RID, with no GA placeholders.
                string path = Path.Combine(directory, "release.json");
                CandidateManifest manifest = await ReleaseFiles.ReadJsonAsync<CandidateManifest>(path).ConfigureAwait(false);
                if (manifest.SchemaVersion != 1 ||
                    manifest.BuildId != buildId.ToString(CultureInfo.InvariantCulture) ||
                    manifest.Commit != commit ||
                    manifest.Variant != variant ||
                    manifest.Placeholder ||
                    !s_rids.Contains(manifest.Rid, StringComparer.Ordinal) ||
                    !rids.Add(manifest.Rid) ||
                    string.IsNullOrEmpty(manifest.Version) ||
                    !VersionPattern().IsMatch(manifest.Version) ||
                    !manifest.Version.Contains($"-{variant}.", StringComparison.Ordinal) ||
                    Path.GetFileName(directory) != manifest.Version)
                {
                    throw new InvalidDataException($"Invalid candidate identity, layout, or provenance: {path}");
                }

                if (version is not null && version != manifest.Version)
                {
                    throw new InvalidDataException("All dotnetup candidates must have the same version.");
                }

                version = manifest.Version;
                string relativeDirectory = Path.GetRelativePath(source, directory).Replace('\\', '/');
                candidates.Add(new SourceCandidate(manifest.Rid, $"{relativeDirectory}/release.json"));

                // Each RID supplies its executable; the Windows x64 candidate also owns both bootstrap scripts.
                List<string> names = [$"dotnetup-{manifest.Rid}{(manifest.Rid.StartsWith("win-", StringComparison.Ordinal) ? ".exe" : "")}"];
                if (manifest.Rid == "win-x64")
                {
                    names.AddRange(["get-dotnetup.ps1", "get-dotnetup.sh"]);
                }

                // Check each payload against its sidecar, and hash the sidecar's original bytes for later verification.
                foreach (string name in names)
                {
                    string payload = Path.Combine(directory, name);
                    string hash = await ReleaseFiles.HashAsync(payload).ConfigureAwait(false);
                    string checksumHash = await ReleaseFiles.HashAsync(payload + ".sha512").ConfigureAwait(false);
                    string checksum = (await File.ReadAllTextAsync(payload + ".sha512").ConfigureAwait(false)).Trim();

                    if (!ChecksumPattern().IsMatch(checksum) || !hash.Equals(checksum, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException($"SHA-512 mismatch: {payload}");
                    }

                    files.Add(new ReleaseAsset(name, $"dotnetup/{version}/{name}", $"{relativeDirectory}/{name}", hash));
                    files.Add(new ReleaseAsset(name + ".sha512", $"dotnetup/{version}/{name}.sha512", $"{relativeDirectory}/{name}.sha512", checksumHash));
                }

                // Reject extra files rather than accidentally publishing manifests, signing metadata, or other content.
                string[] allowed = [.. names, .. names.Select(name => name + ".sha512"), "release.json"];
                if (Directory.EnumerateFileSystemEntries(directory).Any(entry => !allowed.Contains(Path.GetFileName(entry), StringComparer.Ordinal)))
                {
                    throw new InvalidDataException($"Unexpected candidate file: {directory}");
                }
            }
        }

        // A release is complete only when all eight RIDs agree on one selected variant and version.
        if (!rids.Order(StringComparer.Ordinal).SequenceEqual(s_rids, StringComparer.Ordinal))
        {
            throw new InvalidDataException($"Expected exactly one candidate for each RID: {string.Join(", ", s_rids)}.");
        }

        ReleaseFiles.Debug($"Validated {variant} {version}: {candidates.Count} candidates, {files.Count} files.");
        return new ReleaseRecord(buildId, build.GetProperty("buildNumber").GetString()!, commit, variant, version!, [.. candidates], [.. files.OrderBy(file => file.Name, StringComparer.Ordinal)]);
    }

    internal static async Task StageAsync(ReleaseRecord release, string source, string destination)
    {
        // A fresh destination prevents stale files from a previous attempt from entering the release.
        if (Directory.Exists(destination) || File.Exists(destination))
        {
            throw new IOException($"The staging directory must not already exist: {destination}");
        }

        // Recheck all source bytes before creating any staging output to catch changes after manifest validation.
        foreach (ReleaseAsset file in release.Files)
        {
            if (await ReleaseFiles.HashAsync(Path.Combine(source, file.SourceRelativePath)).ConfigureAwait(false) != file.Sha512)
            {
                throw new InvalidDataException($"Source changed after validation: {file.Name}");
            }
        }

        // Copy only the approved inventory into its versioned layout, then verify each staged copy independently.
        Directory.CreateDirectory(Path.Combine(destination, "dotnetup", release.Version));
        foreach (ReleaseAsset file in release.Files)
        {
            string target = Path.Combine(destination, file.RelativePath);
            File.Copy(Path.Combine(source, file.SourceRelativePath), target);

            if (await ReleaseFiles.HashAsync(target).ConfigureAwait(false) != file.Sha512)
            {
                throw new InvalidDataException($"Staged hash mismatch: {file.Name}");
            }

            Console.WriteLine($"Staged {file.RelativePath}: {new FileInfo(target).Length} bytes.");
            ReleaseFiles.Debug($"SHA-512 {file.RelativePath}: {file.Sha512}");
        }
    }

    private static void ValidateBuild(JsonElement build, int buildId, string commit)
    {
        if (build.GetProperty("id").GetInt32() != buildId ||
            !CommitPattern().IsMatch(commit) ||
            build.GetProperty("sourceVersion").GetString() != commit ||
            build.GetProperty("definition").GetProperty("id").GetInt32() != 1544 ||
            build.GetProperty("repository").GetProperty("name").GetString() != "dotnet-sdk" ||
            build.GetProperty("sourceBranch").GetString() != "refs/heads/release/dnup" ||
            build.GetProperty("status").GetString() != "completed" ||
            build.GetProperty("result").GetString() != "succeeded" ||
            !build.GetProperty("templateParameters").TryGetProperty("runTestBuild", out JsonElement testBuild) ||
            !string.Equals(testBuild.ToString(), "false", StringComparison.OrdinalIgnoreCase) ||
            !build.GetProperty("tags").EnumerateArray().Any(tag => tag.GetString() == "PME Signed"))
        {
            throw new InvalidDataException("The source run must be a successful, non-test, PME-signed run of pipeline 1544 on release/dnup with the selected build ID and commit.");
        }

        ReleaseFiles.Debug($"Source build {buildId} eligibility checks passed.");
    }

    [GeneratedRegex(@"^[0-9]+\.[0-9]+\.[0-9]+-(?:preview|daily)\.[0-9]+(?:\.[0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();

    [GeneratedRegex(@"^[0-9a-f]{40}$", RegexOptions.CultureInvariant)]
    private static partial Regex CommitPattern();

    [GeneratedRegex(@"^[0-9a-fA-F]{128}$", RegexOptions.CultureInvariant)]
    private static partial Regex ChecksumPattern();
}
