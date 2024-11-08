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
    /// If this is set to linux-ARCH then we use noble-chiseled for the AOT/Extra/etc decisions.
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

    public bool IsTrimmed { get; set; }

    /// <summary>
    /// If the project is AOT'd the aot image variant doesn't contain ICU and TZData, so we use this flag to see if we need to use the `-extra` variant that does contain those packages.
    /// </summary>
    public bool UsesInvariantGlobalization { get; set; }

    /// <summary>
    /// If set, this expresses a preference for a variant of the container image that we infer for a project.
    /// e.g. 'alpine', or 'noble-chiseled'
    /// </summary>
    public string? ContainerFamily { get; set; }

    /// <summary>
    /// If set, the user has requested a specific base image - in this case we do nothing and echo it out
    /// </summary>
    public string? UserBaseImage { get; set; }

    /// <summary>
    ///  The final base image computed from the inputs (or explicitly set by the user if IsUsingMicrosoftDefaultImages is true)
    /// </summary>
    [Output]
    public string? ComputedContainerBaseImage { get; private set; }

    private bool IsAspNetCoreProject =>
        FrameworkReferences.Length > 0
        && FrameworkReferences.Any(x => x.ItemSpec.Equals("Microsoft.AspNetCore.App", StringComparison.OrdinalIgnoreCase));

    private bool IsMuslRid => TargetRuntimeIdentifier.StartsWith("linux-musl", StringComparison.Ordinal);
    private bool IsBundledRuntime => IsSelfContained;

    private bool RequiresInference => String.IsNullOrEmpty(UserBaseImage);

    // as of March 2024, the -extra images are on stable MCR, but the -aot images are still on nightly. This means AOT, invariant apps need the /nightly/ base.
    private bool NeedsNightlyImages => IsAotPublished && UsesInvariantGlobalization;
    private bool AllowsExperimentalTagInference => String.IsNullOrEmpty(ContainerFamily);

    public ComputeDotnetBaseImageAndTag()
    {
        SdkVersion = "";
        TargetFrameworkVersion = "";
        ContainerFamily = "";
        FrameworkReferences = [];
        TargetRuntimeIdentifier = "";
        UserBaseImage = "";
    }

    public override bool Execute()
    {
        if (!RequiresInference)
        {
            ComputedContainerBaseImage = UserBaseImage;
            LogNoInferencePerformedTelemetry();
            return true;
        }
        else
        {
            var defaultRegistry = RegistryConstants.MicrosoftContainerRegistryDomain;
            if (ComputeRepositoryAndTag(out var repository, out var tag))
            {
                ComputedContainerBaseImage = $"{defaultRegistry}/{repository}:{tag}";
                LogInferencePerformedTelemetry($"{defaultRegistry}/{repository}", tag!);
            }
            return !Log.HasLoggedErrors;
        }
    }

    private string UbuntuCodenameForSDKVersion(SemanticVersion version)
    {
        if (version >= SemanticVersion.Parse("8.0.300"))
        {
            return "noble";
        }
        else
        {
            return "jammy";
        }
    }

    private bool ComputeRepositoryAndTag([NotNullWhen(true)] out string? repository, [NotNullWhen(true)] out string? tag)
    {
        if (ComputeVersionPart() is (string baseVersionPart, SemanticVersion parsedVersion, bool versionAllowsUsingAOTAndExtrasImages))
        {
            var defaultUbuntuVersion = UbuntuCodenameForSDKVersion(parsedVersion);
            Log.LogMessage("Computed base version tag of {0} from TFM {1} and SDK {2}", baseVersionPart, TargetFrameworkVersion, SdkVersion);
            if (baseVersionPart is null)
            {
                repository = null;
                tag = null;
                return false;
            }

            var detectedRepository = (NeedsNightlyImages, IsSelfContained, IsAspNetCoreProject) switch
            {
                (true, true, _) when AllowsExperimentalTagInference && versionAllowsUsingAOTAndExtrasImages => "dotnet/nightly/runtime-deps",
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
                Log.LogMessage("Using user-provided ContainerFamily");

                // we can do one final check here: if the containerfamily is the 'default' for the RID
                // in question, and the app is globalized, we can help and add -extra so the app will actually run

                if (
                    (!IsMuslRid && ContainerFamily!.EndsWith("-chiseled")) // default for linux RID
                    && !UsesInvariantGlobalization
                    && versionAllowsUsingAOTAndExtrasImages
                    // the extras only became available on the stable tags of the FirstVersionWithNewTaggingScheme
                    && (!parsedVersion.IsPrerelease && parsedVersion.Major == FirstVersionWithNewTaggingScheme))
                {
                    Log.LogMessage("Using extra variant because the application needs globalization");
                    tag += "-extra";
                }

                return true;
            }
            else
            {
                if (!versionAllowsUsingAOTAndExtrasImages)
                {
                    tag += IsMuslRid switch
                    {
                        true => "-alpine",
                        false => "" // TODO: should we default here to chiseled images for < 8 apps?
                    };
                    Log.LogMessage("Selected base image tag {0}", tag);
                    return true;
                }
                else
                {
                    // chose the base OS
                    tag += IsMuslRid switch
                    {
                        true => "-alpine",
                        // default to chiseled for AOT, non-musl Apps
                        false when IsAotPublished || IsTrimmed => $"-{defaultUbuntuVersion}-chiseled", // TODO: should we default here to noble-chiseled for non-musl RIDs?
                        // default to noble for non-AOT, non-musl Apps
                        false => ""
                    };

                    // now choose the variant, if any - if globalization then -extra, else -aot
                    tag += (IsAotPublished, IsTrimmed, UsesInvariantGlobalization) switch
                    {
                        (true, _, false) => "-extra",
                        (_, true, false) => "-extra",
                        (true, _, true) => "-aot",
                        _ => ""
                    };
                    Log.LogMessage("Selected base image tag {0}", tag);
                    return true;
                }
            }
        }
        else
        {
            repository = null;
            tag = null;
            return false;
        }
    }

    private (string, SemanticVersion, bool)? ComputeVersionPart()
    {
        if (SemanticVersion.TryParse(TargetFrameworkVersion, out var tfm) && tfm.Major < FirstVersionWithNewTaggingScheme)
        {
            // < 8 TFMs don't support the -aot and -extras images
            return ($"{tfm.Major}.{tfm.Minor}", tfm, false);
        }
        else if (SemanticVersion.TryParse(SdkVersion, out var version))
        {
            if (ComputeVersionInternal(version, tfm) is string majMinor)
            {
                return (majMinor, version, true);
            }
            else
            {
                return null;
            }
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

    private string? DetermineLabelBasedOnChannel(int major, int minor, string[] releaseLabels)
    {
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

    private bool UserImageIsMicrosoftBaseImage => UserBaseImage?.StartsWith("mcr.microsoft.com/") ?? false;

    private void LogNoInferencePerformedTelemetry()
    {
        // we should only log the base image, tag, containerFamily if we _know_ they are MCR images
        string? userBaseImage = null;
        string? userTag = null;
        string? containerFamily = null;
        if (UserBaseImage is not null && UserImageIsMicrosoftBaseImage)
        {
            if (ContainerHelpers.TryParseFullyQualifiedContainerName(UserBaseImage, out var containerRegistry, out var containerName, out var containerTag, out var _, out bool isRegistrySpecified))
            {
                userBaseImage = $"{containerRegistry}/{containerName}";
                userTag = containerTag;
                containerFamily = ContainerFamily;
            }
        }
        var telemetryData = new InferenceTelemetryData(InferencePerformed: false, TargetFramework: ParseSemVerToMajorMinor(TargetFrameworkVersion), userBaseImage, userTag, containerFamily, GetTelemetryProjectType(), GetTelemetryPublishMode(), UsesInvariantGlobalization, TargetRuntimeIdentifier);
        LogTelemetryData(telemetryData);
    }

    private void LogInferencePerformedTelemetry(string imageName, string tag)
    {
        // for all inference use cases we will use .NET's images, so we can safely log name, tag, and family
        var telemetryData = new InferenceTelemetryData(InferencePerformed: true, TargetFramework: ParseSemVerToMajorMinor(TargetFrameworkVersion), imageName, tag, String.IsNullOrEmpty(ContainerFamily) ? null : ContainerFamily, GetTelemetryProjectType(), GetTelemetryPublishMode(), UsesInvariantGlobalization, TargetRuntimeIdentifier);
        LogTelemetryData(telemetryData);
    }

    private PublishMode GetTelemetryPublishMode() => IsAotPublished ? PublishMode.Aot : IsTrimmed ? PublishMode.Trimmed : IsSelfContained ? PublishMode.SelfContained : PublishMode.FrameworkDependent;
    private ProjectType GetTelemetryProjectType() => IsAspNetCoreProject ? ProjectType.AspNetCore : ProjectType.Console;

    private string ParseSemVerToMajorMinor(string semver) => SemanticVersion.Parse(semver).ToString("x.y", VersionFormatter.Instance);

    private void LogTelemetryData(InferenceTelemetryData telemetryData)
    {
        var telemetryProperties = new Dictionary<string, string?>
        {
            { nameof(telemetryData.InferencePerformed), telemetryData.InferencePerformed.ToString() },
            { nameof(telemetryData.TargetFramework), telemetryData.TargetFramework },
            { nameof(telemetryData.BaseImage), telemetryData.BaseImage },
            { nameof(telemetryData.BaseImageTag), telemetryData.BaseImageTag },
            { nameof(telemetryData.ContainerFamily), telemetryData.ContainerFamily },
            { nameof(telemetryData.ProjectType), telemetryData.ProjectType.ToString() },
            { nameof(telemetryData.PublishMode), telemetryData.PublishMode.ToString() },
            { nameof(telemetryData.IsInvariant), telemetryData.IsInvariant.ToString() },
            { nameof(telemetryData.TargetRuntime), telemetryData.TargetRuntime }
        };
        Log.LogTelemetry("sdk/container/inference", telemetryProperties);
    }


    /// <summary>
    /// Telemetry data for the inference task.
    /// </summary>
    /// <param name="InferencePerformed">If the user set an explicit base image or not.</param>
    /// <param name="TargetFramework">The TFM the user was targeting</param>
    /// <param name="BaseImage">If the user specified a Microsoft image or we inferred one, this will be the name of that image. Otherwise null so we can't leak customer data.</param>
    /// <param name="BaseImageTag">If the user specified a Microsoft image or we inferred one, this will be the tag of that image. Otherwise null so we can't leak customer data.</param>
    /// <param name="ContainerFamily">If the user specified a ContainerFamily for our images or we inserted one during inference this will be here. Otherwise null so we can't leak customer data.</param>
    /// <param name="ProjectType">Classifies the project into categories - currently only the broad categories of web/console are known.</param>
    /// <param name="PublishMode">Categorizes the publish mode of the app - FDD, SC, Trimmed, AOT in rough order of complexity/container customization</param>
    /// <param name="IsInvariant">We make inference decisions on the invariant-ness of the project, so it's useful to track how often that is used.</param>
    /// <param name="TargetRuntime">Different RIDs change the inference calculation, so it's useful to know how different RIDs flow into the results of inference.</param>
    private record class InferenceTelemetryData(bool InferencePerformed, string TargetFramework, string? BaseImage, string? BaseImageTag, string? ContainerFamily, ProjectType ProjectType, PublishMode PublishMode, bool IsInvariant, string TargetRuntime);
    private enum ProjectType
    {
        AspNetCore,
        Console
    }
    private enum PublishMode
    {
        FrameworkDependent,
        SelfContained,
        Trimmed,
        Aot
    }

}
