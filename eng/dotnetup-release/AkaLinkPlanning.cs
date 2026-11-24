// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Dotnetup.Release;

internal static partial class AkaLinkPlanning
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

    // Public redirects carry the versioned CDN path, so an accidental downgrade can be detected without downloading binaries.
    internal static void EnsureIntentionalRollback(AkaChangePlan plan, bool rollbackAllowed)
    {
        VersionParts proposed = ParseVersion(plan.Version, plan.Variant)
            ?? throw new InvalidDataException($"Cannot parse selected {plan.Variant} version {plan.Version}.");
        List<string> newer = [];
        List<string> unknown = [];
        foreach (AkaChange change in plan.Changes)
        {
            VersionParts? current = change.PreviousTarget is null ? null : ParseTargetVersion(change.PreviousTarget, plan.Variant, change.Name);
            if (current is null)
            {
                unknown.Add($"{change.ShortUrl} ({change.PreviousTarget ?? change.ObservationError ?? change.ObservationStatus})");
            }
            else if (current.CompareTo(proposed) > 0)
            {
                newer.Add($"{change.ShortUrl} -> {change.PreviousTarget}");
            }
        }

        if (newer.Count == 0 && unknown.Count == 0)
        {
            Console.WriteLine($"Rollback check: no aka.ms link points to a newer {plan.Variant} release than {plan.Version}.");
            return;
        }

        string details = string.Join(Environment.NewLine, [.. newer.Select(link => "  newer: " + link), .. unknown.Select(link => "  unknown: " + link)]);
        if (!rollbackAllowed)
        {
            // Unknown state fails closed; publishing the first release or recovering broken links also requires the explicit override.
            throw new InvalidOperationException(
                $"Refusing to release {plan.Version}: the current release is newer or could not be determined. Set rollback=true if this is intentional.{Environment.NewLine}{details}");
        }

        Console.WriteLine($"Rollback allowed: proceeding with {plan.Version} despite newer or unknown current aka.ms targets.{Environment.NewLine}{details}");
    }

    private static VersionParts? ParseTargetVersion(string target, string variant, string fileName)
    {
        if (!Uri.TryCreate(target, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        string[] segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        int index = Array.LastIndexOf(segments, "dotnetup");
        return index >= 0 && index + 3 == segments.Length && segments[index + 2] == fileName
            ? ParseVersion(segments[index + 1], variant)
            : null;
    }

    private static VersionParts? ParseVersion(string value, string variant)
    {
        Match match = VersionPattern().Match(value);
        if (!match.Success || match.Groups["variant"].Value != variant)
        {
            return null;
        }

        int[] release = new int[3];
        string[] sequenceText = match.Groups["sequence"].Value.Split('.');
        int[] sequence = new int[sequenceText.Length];
        if (!int.TryParse(match.Groups["major"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out release[0]) ||
            !int.TryParse(match.Groups["minor"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out release[1]) ||
            !int.TryParse(match.Groups["patch"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out release[2]) ||
            sequenceText.Where((text, index) => !int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out sequence[index])).Any())
        {
            return null;
        }

        return new VersionParts(release, sequence);
    }

    [GeneratedRegex(@"^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)-(?<variant>daily|preview)\.(?<sequence>\d+(?:\.\d+)*)$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();

    private sealed record VersionParts(int[] Release, int[] Sequence)
    {
        public int CompareTo(VersionParts other)
        {
            int comparison = CompareParts(Release, other.Release);
            return comparison != 0 ? comparison : CompareParts(Sequence, other.Sequence);
        }

        private static int CompareParts(int[] left, int[] right)
        {
            for (int index = 0; index < Math.Max(left.Length, right.Length); index++)
            {
                int comparison = (index < left.Length ? left[index] : 0).CompareTo(index < right.Length ? right[index] : 0);
                if (comparison != 0)
                {
                    return comparison;
                }
            }

            return 0;
        }
    }
}
