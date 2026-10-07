// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Text.Json;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Reads repository SDK settings and normalizes them to a channel with minimum-version
/// and prerelease constraints. Dotnetup always selects the latest eligible version;
/// only disable pins an exact version. The file itself is never changed here.
/// </summary>
internal static class GlobalJsonChannelResolver
{
    private static readonly GlobalJsonContentsJsonContext s_jsonContext = new(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    });

    /// <summary>
    /// Reads the original SDK settings for display as well as requirement evaluation.
    /// Missing SDK versions produce no requirement; malformed settings throw.
    /// </summary>
    public static GlobalJsonContents.SdkSection? ReadSdkSection(string globalJsonPath)
    {
        using var stream = GlobalJsonFileHelper.OpenAsUtf8Stream(globalJsonPath);
        var contents = JsonSerializer.Deserialize(stream, s_jsonContext.GlobalJsonContents)
            ?? throw new JsonException(Strings.GlobalJsonInvalidContents);
        if (contents.Sdk is not { Version: not null } sdk)
        {
            return null;
        }
        var policy = sdk.RollForward?.ToLowerInvariant();
        if (policy is not (null or "disable" or "patch" or "feature" or "minor" or "major"
            or "latestpatch" or "latestfeature" or "latestminor" or "latestmajor"))
        {
            throw new JsonException(string.Format(CultureInfo.InvariantCulture, Strings.GlobalJsonInvalidRollForward, sdk.RollForward));
        }
        if (!ReleaseVersion.TryParse(sdk.Version, out _))
        {
            throw new JsonException(string.Format(CultureInfo.InvariantCulture, Strings.GlobalJsonInvalidSdkVersion, sdk.Version));
        }
        return sdk;
    }

    /// <summary>
    /// Converts validated SDK settings to the same channel model used by standalone specs.
    /// A prerelease request permits prereleases even if allowPrerelease is false.
    /// </summary>
    public static UpdateChannel CreateChannel(GlobalJsonContents.SdkSection sdk)
    {
        var version = new ReleaseVersion(sdk.Version!);
        var name = sdk.RollForward?.ToLowerInvariant() switch
        {
            "disable" => sdk.Version!,
            "feature" or "latestfeature" => string.Create(CultureInfo.InvariantCulture, $"{version.Major}.{version.Minor}"),
            "minor" or "latestminor" => string.Create(CultureInfo.InvariantCulture, $"{version.Major}"),
            "major" or "latestmajor" => "latest",
            // null, patch, and latestPatch all stay within the requested feature band.
            _ => DeriveFeatureBandChannel(version),
        };
        return new UpdateChannel(name, version,
            (sdk.AllowPrerelease ?? true) || !string.IsNullOrEmpty(version.Prerelease));
    }

    /// <summary>
    /// Derives the feature band channel from a parsed SDK version.
    /// For example, "10.0.105" → "10.0.1xx", "9.0.304" → "9.0.3xx".
    /// </summary>
    internal static string DeriveFeatureBandChannel(ReleaseVersion version)
    {
        int featureBand = version.Patch / 100;
        return string.Create(CultureInfo.InvariantCulture, $"{version.Major}.{version.Minor}.{featureBand}xx");
    }
}
