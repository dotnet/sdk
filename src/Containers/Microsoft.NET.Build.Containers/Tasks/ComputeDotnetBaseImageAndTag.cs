// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Microsoft.Build.Framework;
using NuGet.Versioning;
#if NETFRAMEWORK
using System.Linq;
#endif

namespace Microsoft.NET.Build.Containers.Tasks;

/// <summary>
/// Computes the base image and Tag for a Microsoft-authored container image based on the project properties and tagging scheme from various SDK versions.
/// </summary>
public sealed class ComputeDotnetBaseImageAndTag : Microsoft.Build.Utilities.Task
{
    // starting in .NET 8, the container tagging scheme started incorporating the
    // 'channel' (rc/preview) and the channel increment (the numeric value after the channel name)
    // into the container tags.
    private const int FirstVersionWithNewTaggingScheme = 8;

    /// <summary>
    /// When in preview, this influences which preview image tag is used, since previews can have compatibility problems across versions.
    /// </summary>
    [Required]
    public string SdkVersion { get; set; }

    /// <summary>
    /// Used to determine which `tag` of an image should be used by default.
    /// </summary>
    [Required]
    public string TargetFrameworkVersion { get; set; }

    /// <summary>
    /// Used to inspect the project to see if it references ASP.Net Core, which causes a change in base image to dotnet/aspnet.
    /// </summary>
    [Required]
    public ITaskItem[] FrameworkReferences { get; set; }

    /// <summary>
    /// If this is set to linux-ARCH then we use jammy-chiseled for the AOT/Extra/etc decisions.
    /// If this is set to linux-musl-ARCH then we need to use `alpine` for all containers, and tag on `aot` or `extra` as necessary.
    /// </summary>
    [Required]
    public string TargetRuntimeIdentifier { get; set; }


    /// <summary>
    /// If a project is self-contained then it includes a runtime, and so the runtime-deps image should be used.
    /// </summary>
    public bool IsSelfContained { get; set; }
    
    /// <summary>
    /// If a project is AOT-published then not only is it self-contained, but it can also remove some other deps - we can use the dotnet/nightly/runtime-deps variant here aot
    /// </summary>
    public bool IsAotPublished { get; set; }

    /// <summary>
    /// If the project is AOT'd the aot image variant doesn't contain ICU and TZData, so we use this flag to see if we need to use the `-extra` variant that does contain those packages.
    /// </summary>
    public bool UsesInvariantGlobalization { get; set;}

    /// <summary>
    /// If set, this expresses a preference for a variant of the container image that we infer for a project.
    /// e.g. 'alpine', or 'jammy-chiseled'
    /// </summary>
    public string ContainerFamily { get; set; }

    /// <summary>
    ///  The final base image computed from the inputs (or explicitly set by the user if IsUsingMicrosoftDefaultImages is true)
    /// </summary>
    [Output]
    public string? ComputedContainerBaseImage { get; private set; }

    private bool IsAspNetCoreProject => FrameworkReferences.Length > 0 && FrameworkReferences.Any(x => x.ItemSpec.Equals("Microsoft.AspnetCore.App", StringComparison.Ordinal));
    private bool IsMuslRID => TargetRuntimeIdentifier.StartsWith("linux-musl", StringComparison.Ordinal);
    private bool IsBundledRuntime => IsSelfContained;
    private bool NeedsNightlyImages => IsAotPublished;
    private bool AllowsExperimentalTagInference => String.IsNullOrEmpty(ContainerFamily);

    public ComputeDotnetBaseImageAndTag()
    {
        SdkVersion = "";
        TargetFrameworkVersion = "";
        ContainerFamily = "";
        FrameworkReferences = [];
        TargetRuntimeIdentifier = "";
    }

    public override bool Execute()
    {
        var defaultRegistry = "mcr.microsoft.com";
        if (ComputeRepositoryAndTag(out var repository, out var tag))
        {
            ComputedContainerBaseImage = $"{defaultRegistry}/{repository}:{tag}";
        }
        return !Log.HasLoggedErrors;
    }

    private bool ComputeRepositoryAndTag([NotNullWhen(true)] out string? repository, [NotNullWhen(true)] out string? tag)
    {
        var baseVersionPart = ComputeVersionPart();
        Log.LogMessage("Computed base version tag of {0} from TFM {1} and SDK {2}", baseVersionPart, TargetFrameworkVersion, SdkVersion);
        if (baseVersionPart is null) {
            repository = null;
            tag = null;
            return false;
        }

        var detectedRepository = (NeedsNightlyImages, IsSelfContained, IsAspNetCoreProject) switch {
            (true, true, _) when AllowsExperimentalTagInference => "dotnet/nightly/runtime-deps",
            (_, true, _) => "dotnet/runtime-deps",
            (_, _, true) => "dotnet/aspnet",
            (_, _, false) => "dotnet/runtime"
        };
        Log.LogMessage("Chose base image repository {0}", detectedRepository);
        repository = detectedRepository;
        tag = baseVersionPart;
        
        if (!string.IsNullOrWhiteSpace(ContainerFamily))
        {
            // for the inferred image tags, 'family' aka 'flavor' comes after the 'version' portion (including any preview/rc segments).
            // so it's safe to just append here
            tag += $"-{ContainerFamily}";
            return true;
        }
        else 
        {
            tag += (IsAotPublished, UsesInvariantGlobalization, IsMuslRID) switch 
            {
                (true, false, false) => "-jammy-chiseled-extra",
                (true, false, true) => "-alpine-extra",
                (true, true, false) => "-jammy-chiseled-aot",
                (true, true, true) => "-alpine-aot",
                (false, false, true) => "-alpine",
                _ => ""
            };
            Log.LogMessage("Selected base image tag {0}", tag);
            return true;
        }
    }

    private string? ComputeVersionPart()
    {
        if (SemanticVersion.TryParse(TargetFrameworkVersion, out var tfm) && tfm.Major < FirstVersionWithNewTaggingScheme)
        {
            return $"{tfm.Major}.{tfm.Minor}";
        }
        else if (SemanticVersion.TryParse(SdkVersion, out var version))
        {
            return ComputeVersionInternal(version, tfm);
        }
        else
        {
            Log.LogError(Resources.Strings.InvalidSdkVersion, SdkVersion);
            return null;
        }
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
        var baseImageTag = version switch
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
