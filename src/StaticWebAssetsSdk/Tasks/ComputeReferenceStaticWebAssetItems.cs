// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Globalization;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class ComputeReferenceStaticWebAssetItems : Task
{
    [Required]
    public ITaskItem[] Assets { get; set; }

    public ITaskItem[] Patterns { get; set; }

    [Required]
    public string AssetKind { get; set; }

    [Required]
    public string ProjectMode { get; set; }

    [Required]
    public string Source { get; set; }

    public bool UpdateSourceType { get; set; } = true;

    public bool MakeReferencedAssetOriginalItemSpecAbsolute { get; set; }

    /// <summary>
    /// Semicolon-separated glob patterns (e.g. <c>**/*.js;**/*.wasm</c>).
    /// Assets whose <c>RelativePath</c> matches any pattern will have their
    /// <c>SourceType</c> set to <c>Framework</c>.
    /// </summary>
    public string FrameworkPattern { get; set; }

    [Output]
    public ITaskItem[] StaticWebAssets { get; set; }

    [Output]
    public ITaskItem[] DiscoveryPatterns { get; set; }

    public override bool Execute()
    {
        try
        {
            var existingAssets = StaticWebAsset.AssetsByTargetPath(Assets, Source, AssetKind);

            var resultAssets = new List<StaticWebAsset>(existingAssets.Count);
            var groupSet = new HashSet<string>(StringComparer.Ordinal);

            var frameworkMatcher = CreateFrameworkMatcher();
            var matchContext = frameworkMatcher != null ? StaticWebAssetGlobMatcher.CreateMatchContext() : default;

            foreach (var kvp in existingAssets)
            {
                var targetPath = kvp.Key;
                var (selected, all) = kvp.Value;
                if (all != null)
                {
                    // If all assets have distinct, non-empty AssetGroups, they can coexist
                    if (StaticWebAsset.AllAssetsHaveDistinctGroups(all, groupSet))
                    {
                        foreach (var groupedAsset in all)
                        {
                            if (ShouldIncludeAssetAsReference(groupedAsset, out var groupReason))
                            {
                                ApplyFrameworkPattern(groupedAsset, frameworkMatcher, ref matchContext);
                                if (UpdateSourceType && !StaticWebAsset.SourceTypes.IsFramework(groupedAsset.SourceType))
                                {
                                    groupedAsset.SourceType = StaticWebAsset.SourceTypes.Project;
                                }
                                ClearAssetGroupsIfFramework(groupedAsset);
                                if (MakeReferencedAssetOriginalItemSpecAbsolute)
                                {
                                    groupedAsset.OriginalItemSpec = Path.GetFullPath(groupedAsset.OriginalItemSpec);
                                }
                                resultAssets.Add(groupedAsset);
                            }
                            Log.LogMessage(MessageImportance.Low, groupReason);
                        }
                        continue;
                    }

                    Log.LogError("More than one compatible asset found for target path '{0}' -> {1}.",
                        targetPath,
                        Environment.NewLine + string.Join(Environment.NewLine, all.Select(a => $"({a.Identity},{a.AssetKind})")));
                    return false;
                }

                if (ShouldIncludeAssetAsReference(selected, out var reason))
                {
                    ApplyFrameworkPattern(selected, frameworkMatcher, ref matchContext);
                    if (UpdateSourceType && !StaticWebAsset.SourceTypes.IsFramework(selected.SourceType))
                    {
                        selected.SourceType = StaticWebAsset.SourceTypes.Project;
                    }
                    ClearAssetGroupsIfFramework(selected);
                    if (MakeReferencedAssetOriginalItemSpecAbsolute)
                    {
                        selected.OriginalItemSpec = Path.GetFullPath(selected.OriginalItemSpec);
                    }
                    else
                    {
                        selected.OriginalItemSpec = selected.OriginalItemSpec;
                    }
                    resultAssets.Add(selected);
                }
                Log.LogMessage(MessageImportance.Low, reason);
            }

            var patterns = new List<StaticWebAssetsDiscoveryPattern>();
            if (Patterns != null)
            {
                foreach (var pattern in Patterns)
                {
                    if (!StaticWebAssetsDiscoveryPattern.HasSourceId(pattern, Source))
                    {
                        Log.LogMessage(MessageImportance.Low, "Skipping pattern '{0}' because is not defined in the current project.", pattern.ItemSpec);
                    }
                    else
                    {
                        Log.LogMessage(MessageImportance.Low, "Including pattern '{0}' because is defined in the current project.", pattern.ToString());
                        patterns.Add(StaticWebAssetsDiscoveryPattern.FromTaskItem(pattern));
                    }
                }
            }

            StaticWebAssets = resultAssets.Select(a => a.ToTaskItem()).ToArray();
            DiscoveryPatterns = patterns.Select(p => p.ToTaskItem()).ToArray();
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex, showStackTrace: true, showDetail: true, file: null);
        }

        return !Log.HasLoggedErrors;
    }

    private StaticWebAssetGlobMatcher CreateFrameworkMatcher()
    {
        if (string.IsNullOrEmpty(FrameworkPattern))
        {
            return null;
        }

        var patterns = FrameworkPattern
            .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToArray();

        if (patterns.Length == 0)
        {
            return null;
        }

        return new StaticWebAssetGlobMatcherBuilder()
            .AddIncludePatterns(patterns)
            .Build();
    }

    private void ApplyFrameworkPattern(
        StaticWebAsset asset,
        StaticWebAssetGlobMatcher matcher,
        ref StaticWebAssetGlobMatcher.MatchContext matchContext)
    {
        if (matcher == null || !asset.IsDiscovered())
        {
            return;
        }

        var relativePath = StaticWebAssetPathPattern.PathWithoutTokens(asset.RelativePath);
        matchContext.SetPathAndReinitialize(relativePath.AsSpan());
        var match = matcher.Match(matchContext);
        if (match.IsMatch)
        {
            asset.SourceType = StaticWebAsset.SourceTypes.Framework;
            Log.LogMessage(
                MessageImportance.Low,
                "Asset '{0}' with relative path '{1}' matched framework pattern. Updating SourceType to Framework.",
                asset.Identity,
                relativePath);
        }
    }

    private static void ClearAssetGroupsIfFramework(StaticWebAsset asset)
    {
        // Framework assets have already passed group filtering when adopted by the consuming
        // project. Clear AssetGroups so downstream consumers (which may not declare matching
        // StaticWebAssetGroup items) don't inadvertently filter them out.
        if (StaticWebAsset.SourceTypes.IsFramework(asset.SourceType))
        {
            asset.AssetGroups = "";
        }
    }

    private bool ShouldIncludeAssetAsReference(StaticWebAsset candidate, out string reason)
    {
        if (!StaticWebAssetsManifest.ManifestModes.ShouldIncludeAssetAsReference(candidate, ProjectMode))
        {
            reason = string.Format(
                CultureInfo.InvariantCulture,
                "Skipping candidate asset '{0}' because project mode is '{1}' and asset mode is '{2}'",
                candidate.Identity,
                ProjectMode,
                candidate.AssetMode);
            return false;
        }

        reason = string.Format(
            CultureInfo.InvariantCulture,
            "Accepted candidate asset '{0}' because project mode is '{1}' and asset mode is '{2}'",
            candidate.Identity,
            ProjectMode,
            candidate.AssetMode);

        return true;
    }
}
