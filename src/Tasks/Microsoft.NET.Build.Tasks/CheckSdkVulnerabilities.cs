// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Configurer;

namespace Microsoft.NET.Build.Tasks;

/// <summary>
/// MSBuild task that reads cached SDK vulnerability/EOL data and emits
/// NETSDK1238 (vulnerabilities) and NETSDK1239 (EOL) warnings.
/// The cache is populated by the CLI during restore (background, no blocking).
/// This task only reads from disk — it never makes network calls.
/// </summary>
public class CheckSdkVulnerabilities : TaskBase
{
    private const string CacheDirectoryName = "sdk-vulnerability-cache";
    private const string SummaryFilePrefix = "sdk-status-";

    [Required]
    public string SdkVersion { get; set; } = string.Empty;

    protected override void ExecuteCore()
    {
        if (bool.TryParse(Environment.GetEnvironmentVariable(Microsoft.DotNet.Cli.EnvironmentVariableNames.SDK_VULNERABILITY_CHECK_DISABLE), out bool disabled) && disabled)
        {
            return;
        }

        string? userProfileDir = new CliFolderPathCalculatorCore().GetDotnetUserProfileFolderPath();
        if (string.IsNullOrEmpty(userProfileDir))
        {
            return;
        }

        // Validate version to prevent path traversal via malformed version strings
        if (SdkVersion.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return;
        }

        string summaryPath = Path.Combine(userProfileDir, CacheDirectoryName, $"{SummaryFilePrefix}{SdkVersion}.json");
        if (!File.Exists(summaryPath))
        {
            return;
        }

        SdkVulnerabilitySummary? summary;
        try
        {
            string json = File.ReadAllText(summaryPath);
            summary = JsonSerializer.Deserialize(json, SdkVulnerabilitySummaryContext.Default.SdkVulnerabilitySummary);
        }
        catch
        {
            // Corrupt or partially-written cache — skip silently
            return;
        }

        if (summary is null)
        {
            return;
        }

        if (summary.IsEol && summary.EolDate.HasValue)
        {
            Log.LogWarning(
                Strings.SdkVersionIsEol,
                SdkVersion,
                summary.EolDate.Value.ToString("yyyy-MM-dd"));
        }

        if (summary.Cves is { Count: > 0 })
        {
            string cveIds = string.Join(", ", summary.Cves.Where(c => c.Id is not null).Select(c => c.Id));
            if (!string.IsNullOrEmpty(cveIds))
            {
                string upgradeSuffix = summary.LatestSdkVersion is not null
                    ? $" {string.Format(Strings.SdkVersionUpdateRecommendation_Info, summary.LatestSdkVersion)}"
                    : string.Empty;
                Log.LogWarning(
                    Strings.SdkVersionHasVulnerabilities,
                    SdkVersion,
                    cveIds,
                    upgradeSuffix);
            }
        }
    }

    // Local DTOs for deserializing the cached summary.
    // Must stay serialization-compatible with SdkVulnerabilityInfo written by the CLI cache.
    internal sealed class SdkVulnerabilitySummary
    {
        public bool IsEol { get; set; }
        public DateTime? EolDate { get; set; }
        public List<SdkCveSummary>? Cves { get; set; }
        public string? LatestSdkVersion { get; set; }
    }

    internal sealed class SdkCveSummary
    {
        public string? Id { get; set; }
        public string? Url { get; set; }
    }
}

[JsonSerializable(typeof(CheckSdkVulnerabilities.SdkVulnerabilitySummary))]
internal partial class SdkVulnerabilitySummaryContext : JsonSerializerContext
{
}
