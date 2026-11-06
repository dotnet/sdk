// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Microsoft.DotNet.Dotnetup.Release;

// Producer-owned provenance is required explicitly; in particular, an omitted placeholder flag must not imply false.
internal sealed record CandidateManifest(
    [property: JsonRequired] int SchemaVersion,
    [property: JsonRequired] string BuildId,
    [property: JsonRequired] string Commit,
    [property: JsonRequired] string Variant,
    [property: JsonRequired] string Version,
    [property: JsonRequired] string Rid,
    [property: JsonRequired] bool Placeholder);

// Preparation records source evidence separately from the exact versioned inventory that will be uploaded.
internal sealed record SourceCandidate(string Rid, string SourceManifest);
internal sealed record ReleaseAsset(string Name, string RelativePath, string SourceRelativePath, string Sha512);
internal sealed record ReleaseRecord(int AzureDevOpsBuildId, string BuildNumber, string Commit, string Variant, string Version, SourceCandidate[] Candidates, ReleaseAsset[] Files);
// The storage journal tracks partial progress and is not proof that a blob can be downloaded anonymously.
internal sealed record BlobPublishedAsset(string Name, string Sha512, string DownloadUrl, string Status);
internal sealed record BlobPublication(
    int AzureDevOpsBuildId, string BuildNumber, string Commit, string Variant, string Version,
    DateTimeOffset UpdatedAt, string Status, BlobPublishedAsset[] Files);
// These records are produced only after every public download verifies; the CDN names are retained for metadata compatibility.
internal sealed record CdnAsset(string Name, string Sha512, string DownloadUrl);
internal sealed record CdnRelease(int AzureDevOpsBuildId, string BuildNumber, string Commit, string Variant, string Version, CdnAsset[] Files);
// Alias records separate best-effort public observations from verified proposals; neither record authorizes a write.
internal sealed record AkaChange(
    string Name, string ShortUrl, string RelativePath, string Sha512,
    string? PreviousTarget, string ObservationStatus, string? ObservationError,
    string? ProposedTarget, string OperationStatus);
internal sealed record AkaChangePlan(
    int SchemaVersion, bool WritesEnabled, int AzureDevOpsBuildId, string Commit, string Variant, string Version,
    DateTimeOffset ObservedAt, string Status, string[] Blockers, AkaChange[] Changes);
