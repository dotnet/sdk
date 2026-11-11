// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;

namespace Microsoft.DotNet.Dotnetup.Release;

internal static class AkaLinkPlanning
{
    // Observe public redirects in parallel without following them or calling the authenticated management API.
    internal static async Task<AkaChangePlan> CreateAsync(ReleaseRecord release, HttpClient client)
    {
        IEnumerable<Task<AkaChange>> getAkaChanges = release.Files.Select(file => GetAkaChange(release, client, file));
        AkaChange[] changes = await Task.WhenAll(getAkaChanges).ConfigureAwait(false);

        // Proposed targets remain unresolved until all public downloads verify; blockers keep future writes gated.
        return new AkaChangePlan(1, false, release.AzureDevOpsBuildId, release.Commit, release.Variant, release.Version,
            DateTimeOffset.UtcNow, "awaiting-cdn-verification",
            [
                "Aka.ms writes are not implemented or enabled.",
                "Approve aka.ms ownership, authentication, and a separate reviewed enablement change.",
                "Establish bootstrap and self-update checksum compatibility before changing aliases.",
                "Use only verified CDN URLs; publish and verify every file before changing any link.",
                "Public redirect observations are snapshots, not authoritative management records. Re-read authenticated link state before any future write; use PUT only on 200 and POST only on 404.",
                "Link updates are not atomic. Preserve previous targets and record each future operation's outcome for rollback."
            ], changes);
    }

    private static async Task<AkaChange> GetAkaChange(ReleaseRecord release, HttpClient client, ReleaseAsset file)
    {
        string shortUrl = $"https://aka.ms/dotnet/dotnetup/{release.Variant}/{file.Name}";
        string? previous = null;
        string status = "unavailable";
        string? error = null;

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Head, shortUrl);
            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

            Uri? location = response.Headers.Location;
            if ((response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther or
                HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect) &&
                location is { IsAbsoluteUri: true } && location.Scheme == "https" &&
                location.Host != "www.bing.com" && location.Host != "bing.com")
            {
                previous = location.AbsoluteUri;
                status = "observed-public-redirect";
            }
            else
            {
                error = $"Public HEAD returned {(int)response.StatusCode}; no usable target was observed. This does not establish management-service link existence.";
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            // An unavailable snapshot is explicit audit data, not a reason to block independent blob publishing.
            error = exception.Message;
        }

        Console.WriteLine($"Aka.ms snapshot {shortUrl}: {status}; target={previous ?? "unknown"}{(error is null ? "" : $"; {error}")}");
        return new AkaChange(file.Name, shortUrl, file.RelativePath, file.Sha512, previous, status, error, null, "awaiting-verified-cdn-target");
    }

    internal static AkaChangePlan WithVerifiedTargets(AkaChangePlan plan, CdnRelease release)
    {
        // Refuse to attach targets from another build or enable writes while updating the planning metadata.
        if (plan.WritesEnabled ||
            plan.AzureDevOpsBuildId != release.AzureDevOpsBuildId ||
            plan.Commit != release.Commit ||
            plan.Variant != release.Variant ||
            plan.Version != release.Version ||
            plan.Changes.Length != release.Files.Length)
        {
            throw new InvalidDataException("The aka.ms plan must match the verified CDN release and have writes disabled.");
        }

        // Bind each target by both filename and source hash; public redirect snapshots are retained for audit only.
        Dictionary<string, CdnAsset> files = release.Files.ToDictionary(file => file.Name, StringComparer.Ordinal);
        AkaChange[] changes = [.. plan.Changes.Select(change =>
        {
            if (!files.Remove(change.Name, out CdnAsset? file) || file.Sha512 != change.Sha512)
            {
                throw new InvalidDataException($"The aka.ms plan does not match the verified CDN asset: {change.Name}");
            }

            return change with { ProposedTarget = file.DownloadUrl, OperationStatus = "planned-disabled" };
        })];

        return plan with { Status = "cdn-verified-writes-disabled", Changes = changes };
    }
}
