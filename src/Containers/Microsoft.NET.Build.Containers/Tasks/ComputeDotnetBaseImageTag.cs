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
    public string RuntimeFrameworkVersion { get; set; }

    public string ContainerFamily { get; set; }

    [Output]
    public string? ComputedBaseImageTag { get; private set; }

    public ComputeDotnetBaseImageTag()
    {
        ContainerFamily = "";
        RuntimeFrameworkVersion = "";
    }

    public override bool Execute()
    {
        if (SemanticVersion.TryParse(RuntimeFrameworkVersion, out var rfv))
        {
            ComputedBaseImageTag = ComputeVersionInternal(rfv);
        }
        else
        {
            Log.LogError(Resources.Strings.InvalidSdkVersion, RuntimeFrameworkVersion);
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


    private string? ComputeVersionInternal(SemanticVersion version)
    {
        // otherwise if we're in a scenario where we're using the TFM for the given SDK version,
        // and that SDK version may be a prerelease, so we need to handle
        var baseImageTag = (version) switch
        {
            // all stable versions or prereleases with majors before the switch get major/minor tags
            { IsPrerelease: false } or { Major: < FirstVersionWithNewTaggingScheme } => $"{version.Major}.{version.Minor}.{version.Patch}",
            // prereleases after the switch for the first SDK version get major/minor-channel.bump tags
            { IsPrerelease: true, Major: >= FirstVersionWithNewTaggingScheme, Patch: 100 } => DetermineLabelBasedOnChannel(version.Major, version.Minor, version.ReleaseLabels.ToArray()),
            // prereleases of subsequent SDK versions still get to use the stable tags
            { IsPrerelease: true, Major: >= FirstVersionWithNewTaggingScheme } => $"{version.Major}.{version.Minor}",
        };
        return baseImageTag;
    }

    private string? DetermineLabelBasedOnChannel(int major, int minor, string[] releaseLabels)
    {
        // this would be a switch, but we have to support net47x where Range and Index aren't available
        if (releaseLabels.Length == 0)
        {
            return $"{major}.{minor}";
        }
        else
        {
            var channel = releaseLabels[0];
            if (channel == "rc" || channel == "preview")
            {
                if (releaseLabels.Length > 1)
                {
                    // Per the dotnet-docker team, the major.minor preview tag format is a fluke and the major.minor.0 form
                    // should be used for all previews going forward.
                    return $"{major}.{minor}.0-{channel}.{releaseLabels[1]}";
                }
                else
                {
                    Log.LogError(Resources.Strings.InvalidSdkPrereleaseVersion, channel);
                    return null;
                }
            }
            else if (channel == "alpha" || channel == "dev" || channel == "ci")
            {
                return $"{major}.{minor}-preview";
            }
            else
            {
                Log.LogError(Resources.Strings.InvalidSdkPrereleaseVersion, channel);
                return null;
            }
        }
    }
}
