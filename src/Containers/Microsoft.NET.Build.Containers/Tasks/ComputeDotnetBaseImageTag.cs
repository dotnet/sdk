// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using NuGet.Versioning;
#if NETFRAMEWORK
using System.Linq;
#endif

namespace Microsoft.NET.Build.Containers.Tasks;

/// <summary>
/// Computes the base image Tag for a Microsoft-authored container image based on the tagging scheme from various SDK versions.
/// </summary>
public sealed class ComputeDotnetBaseImageTag : Microsoft.Build.Utilities.Task
{
    // starting in .NET 8, the container tagging scheme started incorporating the
    // 'channel' (rc/preview) and the channel increment (the numeric value after the channel name)
    // into the container tags.
    private const int FirstVersionWithNewTaggingScheme = 8;

    [Required]
    public string SdkVersion { get; set; }

    [Required]
    public string TargetFrameworkVersion { get; set; }

    public string ContainerFamily { get; set; }

    [Output]
    public string? ComputedBaseImageTag { get; private set; }

    public ComputeDotnetBaseImageTag()
    {
        SdkVersion = "";
        TargetFrameworkVersion = "";
        ContainerFamily = "";
    }

    public override bool Execute()
    {
        if (SemanticVersion.TryParse(TargetFrameworkVersion, out var tfm) && tfm.Major < FirstVersionWithNewTaggingScheme)
        {
            ComputedBaseImageTag = $"{tfm.Major}.{tfm.Minor}";
        }
        else if (SemanticVersion.TryParse(SdkVersion, out var version))
        {
            ComputedBaseImageTag = ComputeVersionInternal(version, tfm);
        }
        else
        {
            Log.LogError(Resources.Strings.InvalidSdkVersion, SdkVersion);
            return !Log.HasLoggedErrors;
        }

        if (!string.IsNullOrWhiteSpace(ContainerFamily))
        {
            // for the inferred image tags, 'family' aka 'flavor' comes after the 'version' portion (including any preview/rc segments).
            // so it's safe to just append here
            ComputedBaseImageTag += $"-{ContainerFamily}";
        }
        return true;
    }


    private string? ComputeVersionInternal(SemanticVersion version, SemanticVersion? tfm)
    {
        if (tfm != null && (tfm.Major < version.Major || tfm.Minor < version.Minor))
        {
            // in this case the TFM is earlier, so we are assumed to be in a stable scenario
            return $"{tfm.Major}.{tfm.Minor}";
        }
        // otherwise if we're in a scenario where we're using the TFM for the given SDK version,
        // and that SDK version may be a prerelease, so we need to handle
        var baseImageTag = (version) switch
        {
            // all stable versions or prereleases with majors before the switch get major/minor tags
            { IsPrerelease: false } or { Major: < FirstVersionWithNewTaggingScheme } => $"{version.Major}.{version.Minor}",
            // prereleases after the switch for the first SDK version get major/minor-channel.bump tags
            { IsPrerelease: true, Major: >= FirstVersionWithNewTaggingScheme, Patch: 100 } => DetermineLabelBasedOnChannel(version.Major, version.Minor, version.ReleaseLabels.ToArray()),
            // prereleases of subsequent SDK versions still get to use the stable tags
            { IsPrerelease: true, Major: >= FirstVersionWithNewTaggingScheme } => $"{version.Major}.{version.Minor}",
        };
        return baseImageTag;
    }

     private string? DetermineLabelBasedOnChannel(int major, int minor, string[] releaseLabels) {
        var channel = releaseLabels.Length > 0 ? releaseLabels[0] : null;
        switch (channel)
        {
            case null or "rtm" or "servicing":
                return $"{major}.{minor}";
            case "rc" or "preview":
                if (releaseLabels.Length > 1)
                {
                    // Per the dotnet-docker team, the major.minor preview tag format is a fluke and the major.minor.0 form
                    // should be used for all previews going forward.
                    return $"{major}.{minor}.0-{channel}.{releaseLabels[1]}";
                }
                Log.LogError(Resources.Strings.InvalidSdkPrereleaseVersion, channel);
                return null;
            case "alpha" or "dev" or "ci":
                return $"{major}.{minor}-preview";
            default:
                Log.LogError(Resources.Strings.InvalidSdkPrereleaseVersion, channel);
                return null;
        };
     }
}
