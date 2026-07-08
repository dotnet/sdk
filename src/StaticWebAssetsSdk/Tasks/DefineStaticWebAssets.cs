// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

// Define static web asset builds a normalized static web asset out of a set of candidate assets.
// The BaseAssets might already define all the properties the assets care about and in that case,
// those properties are preserved and reused. If some other properties are not present, default
// values are used when possible. Otherwise, the properties need to be specified explicitly on the task or
// else an error is generated. The property overrides allows the caller to override the existing value for a property
// with the provided value.
// There is an asset pattern filter that can be used to apply the pattern to a subset of the candidate assets
// which allows for applying a different set of values to a subset of the candidates without having to previously
// filter them. The asset pattern filter is applied after we've determined the RelativePath for the candidate asset.
// There is also a RelativePathPattern that is used to automatically transform the relative path of the candidates to match
// the expected path of the final asset. This is typically use to remove a common path prefix, like `wwwroot` from the target
// path of the assets and so on.
public partial class DefineStaticWebAssets : Task
{
    private static readonly char[] GroupPatternSeparator = [';'];

    [Required]
    public ITaskItem[] CandidateAssets { get; set; }

    public string[] PropertyOverrides { get; set; }

    public string SourceId { get; set; }

    public string SourceType { get; set; }

    public string BasePath { get; set; }

    public string ContentRoot { get; set; }

    public string RelativePathPattern { get; set; }

    public ITaskItem[] FingerprintPatterns { get; set; }

    public bool FingerprintCandidates { get; set; }

    public string RelativePathFilter { get; set; }

    public string AssetKind { get; set; } = StaticWebAsset.AssetKinds.All;

    public string AssetMode { get; set; } = StaticWebAsset.AssetModes.All;

    public string AssetRole { get; set; } = StaticWebAsset.AssetRoles.Primary;

    public string AssetMergeSource { get; set; } = "";

    public string AssetMergeBehavior { get; set; } = StaticWebAsset.MergeBehaviors.None;

    public string RelatedAsset { get; set; }

    public string AssetTraitName { get; set; }

    public string AssetTraitValue { get; set; }

    public string CopyToOutputDirectory { get; set; } = StaticWebAsset.AssetCopyOptions.Never;

    public string CopyToPublishDirectory { get; set; } = StaticWebAsset.AssetCopyOptions.PreserveNewest;

    public string CacheManifestPath { get; set; }

    public ITaskItem[] StaticWebAssetGroupDefinitions { get; set; }

    [Output]
    public ITaskItem[] Assets { get; set; }

    [Output]
    public ITaskItem[] CopyCandidates { get; set; }

    public Func<string, string, (FileInfo file, long fileLength, DateTimeOffset lastWriteTimeUtc)> TestResolveFileDetails { get; set; }

    private HashSet<string> _overrides;

    public override bool Execute()
    {
        _overrides = new HashSet<string>(PropertyOverrides ?? [], StringComparer.OrdinalIgnoreCase);

        // Parse group definitions once upfront so they can be applied per-asset inside the loop.
        Dictionary<string, List<GroupDefinition>> groupDefinitions = null;
        if (StaticWebAssetGroupDefinitions != null && StaticWebAssetGroupDefinitions.Length > 0)
        {
            groupDefinitions = ParseGroupDefinitions();
            if (groupDefinitions == null)
            {
                return false; // Validation error already logged
            }
            Log.LogMessage(MessageImportance.Low, "Parsed {0} group definition source(s).", groupDefinitions.Count);
        }

        var assetsCache = GetOrCreateAssetsCache();

        if (assetsCache.IsUpToDate())
        {
            var outputs = assetsCache.GetComputedOutputs();
            Assets = [.. outputs.Assets];
            CopyCandidates = [.. outputs.CopyCandidates];
        }
        else
        {
            try
            {
            var matcher = !string.IsNullOrEmpty(RelativePathPattern) ?
                new StaticWebAssetGlobMatcherBuilder().AddIncludePatterns(RelativePathPattern).Build() :
                null;

            var filter = !string.IsNullOrEmpty(RelativePathFilter) ?
                new StaticWebAssetGlobMatcherBuilder().AddIncludePatterns(RelativePathFilter).Build() :
                null;

            var assetsByRelativePath = new Dictionary<string, (ITaskItem First, ITaskItem Second)>(CandidateAssets.Length);
            var fingerprintPatternMatcher = new FingerprintPatternMatcher(Log, FingerprintCandidates ? (FingerprintPatterns ?? []) : []);
            var matchContext = StaticWebAssetGlobMatcher.CreateMatchContext();
            foreach (var kvp in assetsCache.OutOfDateInputs())
            {
                var hash = kvp.Key;
                var candidate = kvp.Value;
                var relativePathCandidate = string.Empty;
                if (SourceType == StaticWebAsset.SourceTypes.Discovered)
                {
                    var candidateMatchPath = GetDiscoveryCandidateMatchPath(candidate);
                    if (Path.IsPathRooted(candidateMatchPath) && candidateMatchPath == candidate.ItemSpec)
                    {
                        var normalizedAssetPath = Path.GetFullPath(candidate.GetMetadata("FullPath"));
                        var normalizedDirectoryPath = Path.GetDirectoryName(BuildEngine.ProjectFileOfTaskNode);
                        if (normalizedAssetPath.StartsWith(normalizedDirectoryPath))
                        {
                            var directoryPathLength = normalizedDirectoryPath switch
                            {
                                null => 0,
                                "" => 0,
#pragma warning disable IDE0056 // Indexers are not available. in .NET Framework
                                var withSeparator when withSeparator[withSeparator.Length - 1] == Path.DirectorySeparatorChar || withSeparator[withSeparator.Length - 1] == Path.AltDirectorySeparatorChar => normalizedDirectoryPath.Length,
                                _ => normalizedDirectoryPath.Length + 1
                            };
#pragma warning restore IDE0056
                            var result = normalizedAssetPath.Substring(directoryPathLength);
                            Log.LogMessage(MessageImportance.Low, "FullPath '{0}' starts with content root '{1}' for candidate '{2}'. Using '{3}' as relative path.", normalizedAssetPath, normalizedDirectoryPath, candidate.ItemSpec, result);
                            candidateMatchPath = result;
                        }
                    }
                    relativePathCandidate = candidateMatchPath;
                    if (matcher != null && string.IsNullOrEmpty(candidate.GetMetadata("RelativePath")))
                    {
                        matchContext.SetPathAndReinitialize(StaticWebAssetPathPattern.PathWithoutTokens(candidateMatchPath));
                        var match = matcher.Match(matchContext);
                        if (!match.IsMatch)
                        {
                            Log.LogMessage(MessageImportance.Low, "Rejected asset '{0}' for pattern '{1}'", candidateMatchPath, RelativePathPattern);
                            continue;
                        }

                        Log.LogMessage(MessageImportance.Low, "Accepted asset '{0}' for pattern '{1}' with relative path '{2}'", candidateMatchPath, RelativePathPattern, match.Stem);

                        relativePathCandidate = StaticWebAsset.Normalize(match.Stem);
                    }
                }
                else
                {
                    relativePathCandidate = GetCandidateMatchPath(candidate);
                    if (matcher != null)
                    {
                        matchContext.SetPathAndReinitialize(StaticWebAssetPathPattern.PathWithoutTokens(relativePathCandidate));
                        var match = matcher.Match(matchContext);
                        if (match.IsMatch)
                        {
                            var newRelativePathCandidate = match.Stem;
                            Log.LogMessage(
                                MessageImportance.Low,
                                "The relative path '{0}' matched the pattern '{1}'. Replacing relative path with '{2}'.",
                                relativePathCandidate,
                                RelativePathPattern,
                                newRelativePathCandidate);

                            relativePathCandidate = newRelativePathCandidate;
                        }
                    }

                    if (filter != null)
                    {
                        matchContext.SetPathAndReinitialize(StaticWebAssetPathPattern.PathWithoutTokens(relativePathCandidate));
                        if (!filter.Match(matchContext).IsMatch)
                        {
                            Log.LogMessage(
                                MessageImportance.Low,
                                "Skipping '{0}' because the relative path '{1}' did not match the filter '{2}'.",
                                candidate.ItemSpec,
                                relativePathCandidate,
                                RelativePathFilter);

                            continue;
                        }
                    }
                }

                var sourceId = ComputePropertyValue(candidate, nameof(StaticWebAsset.SourceId), SourceId);
                var sourceType = ComputePropertyValue(candidate, nameof(StaticWebAsset.SourceType), SourceType);
                var basePath = ComputePropertyValue(candidate, nameof(StaticWebAsset.BasePath), BasePath);
                var contentRoot = ComputePropertyValue(candidate, nameof(StaticWebAsset.ContentRoot), ContentRoot);
                var assetKind = ComputePropertyValue(candidate, nameof(StaticWebAsset.AssetKind), AssetKind, isRequired: false);
                var assetMode = ComputePropertyValue(candidate, nameof(StaticWebAsset.AssetMode), AssetMode);
                var assetRole = ComputePropertyValue(candidate, nameof(StaticWebAsset.AssetRole), AssetRole);
                var assetMergeSource = ComputePropertyValue(candidate, nameof(StaticWebAsset.AssetMergeSource), AssetMergeSource);
                var relatedAsset = ComputePropertyValue(candidate, nameof(StaticWebAsset.RelatedAsset), RelatedAsset, !StaticWebAsset.AssetRoles.IsPrimary(assetRole));
                var assetTraitName = ComputePropertyValue(candidate, nameof(StaticWebAsset.AssetTraitName), AssetTraitName, !StaticWebAsset.AssetRoles.IsPrimary(assetRole));
                var assetTraitValue = ComputePropertyValue(candidate, nameof(StaticWebAsset.AssetTraitValue), AssetTraitValue, !StaticWebAsset.AssetRoles.IsPrimary(assetRole));

                var copyToOutputDirectory = ComputePropertyValue(candidate, nameof(StaticWebAsset.CopyToOutputDirectory), CopyToOutputDirectory);
                var copyToPublishDirectory = ComputePropertyValue(candidate, nameof(StaticWebAsset.CopyToPublishDirectory), CopyToPublishDirectory);
                var originalItemSpec = ComputePropertyValue(
                    candidate,
                    nameof(StaticWebAsset.OriginalItemSpec),
                    PropertyOverrides == null || PropertyOverrides.Length == 0 ? candidate.ItemSpec : candidate.GetMetadata("OriginalItemSpec"));

                // Compute the fingerprint and integrity for the asset. The integrity is the Base64(SHA256) of the asset content
                // and the fingerprint is the first 9 chars of the Base36(SHA256) of the asset.
                // The hash can always be re-computed using the integrity value (just undo the Base64 encoding) if its needed in any
                // other format.
                // We differentiate between Integrity and Fingerprint because they are useful in different contexts. The integrity
                // is useful when we want to verify the content of the asset and the fingerprint is useful when we want to cache-bust
                // the asset.
                var fingerprint = ComputePropertyValue(candidate, nameof(StaticWebAsset.Fingerprint), null, false);
                var integrity = ComputePropertyValue(candidate, nameof(StaticWebAsset.Integrity), null, false);

                var identity = Path.GetFullPath(candidate.GetMetadata("FullPath"));
                var (file, fileLength, lastWriteTimeUtc) = ResolveFileDetails(originalItemSpec, identity);

                switch ((fingerprint, integrity))
                {
                    case (null, null):
                        Log.LogMessage(MessageImportance.Low, "Computing fingerprint and integrity for asset '{0}'", candidate.ItemSpec);
                        (fingerprint, integrity) = (StaticWebAsset.ComputeFingerprintAndIntegrity(file));
                        break;
                    case (null, not null):
                        Log.LogMessage(MessageImportance.Low, "Computing fingerprint for asset '{0}'", candidate.ItemSpec);
                        fingerprint = FileHasher.ToBase36(Convert.FromBase64String(integrity));
                        break;
                    case (not null, null):
                        Log.LogMessage(MessageImportance.Low, "Computing integrity for asset '{0}'", candidate.ItemSpec);
                        integrity = StaticWebAsset.ComputeIntegrity(file);
                        break;
                }

                // If we are not able to compute the value based on an existing value or a default, we produce an error and stop.
                if (Log.HasLoggedErrors)
                {
                    break;
                }

                // IMPORTANT: Apply fingerprint pattern (which can change the file name) BEFORE computing identity
                // for non-Discovered assets so that a synthesized identity incorporates the fingerprint pattern.
                if (FingerprintCandidates)
                {
                    matchContext.SetPathAndReinitialize(relativePathCandidate);
                    relativePathCandidate = StaticWebAsset.Normalize(fingerprintPatternMatcher.AppendFingerprintPattern(matchContext, identity));
                }

                if (!string.Equals(SourceType, StaticWebAsset.SourceTypes.Discovered, StringComparison.OrdinalIgnoreCase))
                {
                    // We ignore the content root for publish only assets since it doesn't matter.
                    var contentRootPrefix = StaticWebAsset.AssetKinds.IsPublish(assetKind) ? null : contentRoot;
                    (identity, var computed) = ComputeCandidateIdentity(candidate, contentRootPrefix, relativePathCandidate, matcher, matchContext);

                    if (computed)
                    {
                        // If we synthesized identity and there is a fingerprint placeholder pattern in the file name
                        // expand it to the concrete fingerprinted file name while keeping RelativePath pattern form.
                        if (FingerprintCandidates && !string.IsNullOrEmpty(fingerprint))
                        {
                            var fileNamePattern = Path.GetFileName(identity);
                            if (fileNamePattern.Contains("#["))
                            {
                                var expanded = StaticWebAssetPathPattern.ExpandIdentityFileNameForFingerprint(fileNamePattern, fingerprint);
                                identity = Path.Combine(Path.GetDirectoryName(identity) ?? string.Empty, expanded);
                            }
                        }
                        assetsCache.AppendCopyCandidate(hash, candidate.ItemSpec, identity);
                    }
                }

                var asset = StaticWebAsset.FromProperties(
                    identity,
                    sourceId,
                    sourceType,
                    basePath,
                    relativePathCandidate,
                    contentRoot,
                    assetKind,
                    assetMode,
                    assetRole,
                    assetMergeSource,
                    relatedAsset,
                    assetTraitName,
                    assetTraitValue,
                    fingerprint,
                    integrity,
                    copyToOutputDirectory,
                    copyToPublishDirectory,
                    originalItemSpec,
                    fileLength,
                    lastWriteTimeUtc);

                // Preserve AssetGroups from the candidate if it already has one (e.g., compressed alternative
                // inheriting from its primary asset)
                var existingGroups = candidate.GetMetadata(nameof(StaticWebAsset.AssetGroups));
                if (!string.IsNullOrEmpty(existingGroups))
                {
                    asset.AssetGroups = existingGroups;
                }

                // Capture the pre-group relative path for dedup — group definitions may rewrite
                // RelativePath so that two grouped assets (e.g. V4/css/site.css, V5/css/site.css)
                // share the same post-group path. Dedup must use the original path.
                var dedupRelativePath = asset.RelativePath;

                // Apply group definitions to this individual asset before serializing to ITaskItem.
                if (groupDefinitions != null)
                {
                    ApplyGroupToAsset(ref asset, groupDefinitions, matchContext);
                    if (Log.HasLoggedErrors)
                    {
                        break;
                    }
                }

                var item = asset.ToTaskItem();
                if (SourceType == StaticWebAsset.SourceTypes.Discovered)
                {
                    item.SetMetadata(nameof(StaticWebAsset.AssetKind), !asset.ShouldCopyToPublishDirectory() ? StaticWebAsset.AssetKinds.Build : StaticWebAsset.AssetKinds.All);
                    UpdateAssetKindIfNecessary(assetsByRelativePath, dedupRelativePath, item);
                }

                assetsCache.AppendAsset(hash, asset, item);
            }

            var outputs = assetsCache.GetComputedOutputs();
            var results = outputs.Assets;

            assetsCache.WriteCacheManifest();

            Assets = [.. outputs.Assets];
            CopyCandidates = [.. outputs.CopyCandidates];
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex);
            }
        }

        return !Log.HasLoggedErrors;
    }

    private (FileInfo file, long fileLength, DateTimeOffset lastWriteTimeUtc) ResolveFileDetails(
        string originalItemSpec,
        string identity)
    {
        if (TestResolveFileDetails != null)
        {
            return TestResolveFileDetails(identity, originalItemSpec);
        }
        var file = StaticWebAsset.ResolveFile(identity, originalItemSpec);
        var fileLength = file.Length;
        var lastWriteTimeUtc = file.LastWriteTimeUtc;
        return (file, fileLength, lastWriteTimeUtc);
    }

    private (string identity, bool computed) ComputeCandidateIdentity(
        ITaskItem candidate,
        string contentRoot,
        string relativePath,
        StaticWebAssetGlobMatcher matcher,
        StaticWebAssetGlobMatcher.MatchContext matchContext)
    {
        var candidateFullPath = Path.GetFullPath(candidate.GetMetadata("FullPath"));
        if (contentRoot == null)
        {
            Log.LogMessage(MessageImportance.Low, "Identity for candidate '{0}' is '{1}' because content root is not defined.", candidate.ItemSpec, candidateFullPath);
            return (candidateFullPath, false);
        }

        var normalizedContentRoot = StaticWebAsset.NormalizeContentRootPath(contentRoot);
        if (candidateFullPath.StartsWith(normalizedContentRoot))
        {
            Log.LogMessage(MessageImportance.Low, "Identity for candidate '{0}' is '{1}' because it starts with content root '{2}'.", candidate.ItemSpec, candidateFullPath, normalizedContentRoot);
            return (candidateFullPath, false);
        }
        else
        {
            // We want to support assets that are part of the source codebase but that might get transformed during the build or
            // publish processes, so we want to allow defining these assets by setting up a different content root path from their
            // original location in the project. For example the asset can be wwwroot\my-prod-asset.js, the content root can be
            // obj\transform and the final asset identity can be <<FullPathTo>>\obj\transform\my-prod-asset.js
            GlobMatch matchResult = default;
            if (matcher != null)
            {
                matchContext.SetPathAndReinitialize(StaticWebAssetPathPattern.PathWithoutTokens(candidate.ItemSpec));
                matchResult = matcher.Match(matchContext);
            }
            if (matcher == null)
            {
                // If no relative path pattern was specified, we are going to suggest that the identity is `%(ContentRoot)\RelativePath\OriginalFileName`
                // We don't want to use the relative path file name since multiple assets might map to that and conflicts might arise.
                // Alternatively, we could be explicit here and support ContentRootSubPath to indicate where it needs to go.
                var identitySubPath = Path.GetDirectoryName(relativePath);
                var itemSpecFileName = Path.GetFileName(candidateFullPath);
                var relativeFileName = Path.GetFileName(relativePath);
                // If the relative path filename has been modified (e.g. fingerprint pattern appended) use it when synthesizing identity.
                if (!string.IsNullOrEmpty(relativeFileName) && !string.Equals(relativeFileName, itemSpecFileName, StringComparison.OrdinalIgnoreCase))
                {
                    itemSpecFileName = relativeFileName;
                }
                var finalIdentity = Path.Combine(normalizedContentRoot, identitySubPath ?? string.Empty, itemSpecFileName);
                Log.LogMessage(MessageImportance.Low, "Identity for candidate '{0}' is '{1}' because it did not start with the content root '{2}'", candidate.ItemSpec, finalIdentity, normalizedContentRoot);
                return (finalIdentity, true);
            }
            else if (!matchResult.IsMatch)
            {
                Log.LogMessage(MessageImportance.Low, "Identity for candidate '{0}' is '{1}' because it didn't match the relative path pattern", candidate.ItemSpec, candidateFullPath);
                return (candidateFullPath, false);
            }
            else
            {
                var stem = matchResult.Stem;
                var assetIdentity = Path.GetFullPath(Path.Combine(normalizedContentRoot, stem));
                Log.LogMessage(MessageImportance.Low, "Computed identity '{0}' for candidate '{1}'", assetIdentity, candidate.ItemSpec);

                return (assetIdentity, true);
            }
        }
    }

    private string ComputePropertyValue(ITaskItem element, string metadataName, string propertyValue, bool isRequired = true)
    {
        if (_overrides.Contains(metadataName))
        {
            return propertyValue;
        }

        var value = element.GetMetadata(metadataName);
        if (string.IsNullOrEmpty(value))
        {
            if (propertyValue == null && isRequired)
            {
                Log.LogError("No metadata '{0}' was present for item '{1}' and no default value was provided.",
                    metadataName,
                    element.ItemSpec);

                return null;
            }
            else
            {
                return propertyValue;
            }
        }
        else
        {
            return value;
        }
    }

    private string GetCandidateMatchPath(ITaskItem candidate)
    {
        var relativePath = candidate.GetMetadata("RelativePath");
        if (!string.IsNullOrEmpty(relativePath))
        {
            var normalizedPath = StaticWebAsset.Normalize(relativePath, allowEmpyPath: true);
            Log.LogMessage(MessageImportance.Low, "RelativePath '{0}' normalized to '{1}' found for candidate '{2}' and will be used for matching.", relativePath, normalizedPath, candidate.ItemSpec);
            return normalizedPath;
        }

        var targetPath = candidate.GetMetadata("TargetPath");
        if (!string.IsNullOrEmpty(targetPath))
        {
            var normalizedPath = StaticWebAsset.Normalize(targetPath, allowEmpyPath: true);
            Log.LogMessage(MessageImportance.Low, "TargetPath '{0}' normalized to '{1}' found for candidate '{2}' and will be used for matching.", targetPath, normalizedPath, candidate.ItemSpec);
            return normalizedPath;
        }

        var linkPath = candidate.GetMetadata("Link");
        if (!string.IsNullOrEmpty(linkPath))
        {
            var normalizedPath = StaticWebAsset.Normalize(linkPath, allowEmpyPath: true);
            Log.LogMessage(MessageImportance.Low, "Link '{0}'  normalized to '{1}' found for candidate '{2}' and will be used for matching.", linkPath, normalizedPath, candidate.ItemSpec);

            return linkPath;
        }

        var normalizedContentRoot = StaticWebAsset.NormalizeContentRootPath(string.IsNullOrEmpty(candidate.GetMetadata(nameof(StaticWebAsset.ContentRoot))) ?
            ContentRoot :
            candidate.GetMetadata(nameof(StaticWebAsset.ContentRoot)));

        var normalizedAssetPath = Path.GetFullPath(candidate.GetMetadata("FullPath"));
        if (normalizedAssetPath.StartsWith(normalizedContentRoot))
        {
            var result = normalizedAssetPath.Substring(normalizedContentRoot.Length);
            Log.LogMessage(MessageImportance.Low, "FullPath '{0}' starts with content root '{1}' for candidate '{2}'. Using '{3}' as relative path.", normalizedAssetPath, normalizedContentRoot, candidate.ItemSpec, result);
            return result;
        }
        else
        {
            Log.LogMessage("No relative path, target path or link was found for candidate '{0}'. FullPath '{0}' does not start with content root '{1}' for candidate '{2}'. Using item spec '{2}' as relative path.", normalizedAssetPath, normalizedContentRoot, candidate.ItemSpec);
            return candidate.ItemSpec;
        }
    }

    private void UpdateAssetKindIfNecessary(
        Dictionary<string, (ITaskItem First, ITaskItem Second)> assetsByRelativePath,
        string candidateRelativePath, ITaskItem asset)
    {
        // We want to support content items in the form of
        // <Content Include="service-worker.development.js CopyToPublishDirectory="Never" TargetPath="wwwroot\service-worker.js" />
        // <Content Include="service-worker.js />
        // where the first item is used during development and the second item is used when the app is published.
        // To that matter, we keep track of the assets relative paths and make sure that when two assets target the same relative paths, at least one
        // of them is marked with CopyToPublishDirectory="Never" to identify it as a "development/build" time asset as opposed to the other asset.
        // As a result, assets by default have an asset kind 'All' when there is only one asset for the target path and 'Build' or 'Publish' when there are two of them.
        if (!assetsByRelativePath.TryGetValue(candidateRelativePath, out var existing))
        {
            assetsByRelativePath.Add(candidateRelativePath, (asset, null));
        }
        else
        {
            var (first, second) = existing;
            if (first != null && second != null)
            {
                var errorMessage = "More than two assets are targeting the same path: " + Environment.NewLine +
                    "'{0}' with kind '{1}'" + Environment.NewLine +
                    "'{2}' with kind '{3}'" + Environment.NewLine +
                    "for path '{4}'";

                Log.LogError(
                    errorMessage,
                    first.GetMetadata("FullPath"),
                    first.GetMetadata(nameof(StaticWebAsset.AssetKind)),
                    second.GetMetadata("FullPath"),
                    second.GetMetadata(nameof(StaticWebAsset.AssetKind)),
                    candidateRelativePath);

                return;
            }
            else if (first != null && second == null)
            {
                var existingAsset = first;
                switch ((asset.GetMetadata(nameof(StaticWebAsset.CopyToPublishDirectory)), existingAsset.GetMetadata(nameof(StaticWebAsset.CopyToPublishDirectory))))
                {
                    case (StaticWebAsset.AssetCopyOptions.Never, StaticWebAsset.AssetCopyOptions.Never):
                    case (not StaticWebAsset.AssetCopyOptions.Never, not StaticWebAsset.AssetCopyOptions.Never):
                        var errorMessage = "Two assets found targeting the same path with incompatible asset kinds:" + Environment.NewLine +
                            "'{0}' with kind '{1}'" + Environment.NewLine +
                            "'{2}' with kind '{3}'" + Environment.NewLine +
                            "for path '{4}'";
                        Log.LogError(
                            errorMessage,
                            existingAsset.GetMetadata("FullPath"),
                            existingAsset.GetMetadata(nameof(StaticWebAsset.AssetKind)),
                            asset.GetMetadata("FullPath"),
                            asset.GetMetadata(nameof(StaticWebAsset.AssetKind)),
                            candidateRelativePath);

                        break;

                    case (StaticWebAsset.AssetCopyOptions.Never, not StaticWebAsset.AssetCopyOptions.Never):
                        existing.Second = asset;
                        asset.SetMetadata(nameof(StaticWebAsset.AssetKind), StaticWebAsset.AssetKinds.Build);
                        existingAsset.SetMetadata(nameof(StaticWebAsset.AssetKind), StaticWebAsset.AssetKinds.Publish);
                        Log.LogMessage(MessageImportance.Low,
                            "Asset '{0}' with kind '{1}' and CopyToPublishDirectory='{2}' was found for path '{3}'. Setting asset kind to '{4}'.",
                            asset.GetMetadata("FullPath"),
                            asset.GetMetadata(nameof(StaticWebAsset.AssetKind)),
                            asset.GetMetadata(nameof(StaticWebAsset.CopyToPublishDirectory)),
                            candidateRelativePath,
                            StaticWebAsset.AssetKinds.Build);
                        Log.LogMessage(MessageImportance.Low,
                            "Asset '{0}' with kind '{1}' and CopyToPublishDirectory='{2}' was found for path '{3}'. Setting asset kind to '{4}'.",
                            existingAsset.GetMetadata("FullPath"),
                            existingAsset.GetMetadata(nameof(StaticWebAsset.AssetKind)),
                            existingAsset.GetMetadata(nameof(StaticWebAsset.CopyToPublishDirectory)),
                            candidateRelativePath,
                            StaticWebAsset.AssetKinds.Publish);
                        break;

                    case (not StaticWebAsset.AssetCopyOptions.Never, StaticWebAsset.AssetCopyOptions.Never):
                        existing.Second = asset;
                        asset.SetMetadata(nameof(StaticWebAsset.AssetKind), StaticWebAsset.AssetKinds.Publish);
                        existingAsset.SetMetadata(nameof(StaticWebAsset.AssetKind), StaticWebAsset.AssetKinds.Build);
                        Log.LogMessage(MessageImportance.Low,
                            "Asset '{0}' with kind '{1}' and CopyToPublishDirectory='{2}' was found for path '{3}'. Setting asset kind to '{4}'.",
                            asset.GetMetadata("FullPath"),
                            asset.GetMetadata(nameof(StaticWebAsset.AssetKind)),
                            asset.GetMetadata(nameof(StaticWebAsset.CopyToPublishDirectory)),
                            candidateRelativePath,
                            StaticWebAsset.AssetKinds.Publish);
                        Log.LogMessage(MessageImportance.Low,
                            "Asset '{0}' with kind '{1}' and CopyToPublishDirectory='{2}' was found for path '{3}'. Setting asset kind to '{4}'.",
                            existingAsset.GetMetadata("FullPath"),
                            existingAsset.GetMetadata(nameof(StaticWebAsset.AssetKind)),
                            existingAsset.GetMetadata(nameof(StaticWebAsset.CopyToPublishDirectory)),
                            candidateRelativePath,
                            StaticWebAsset.AssetKinds.Build);
                        break;
                }
            }
        }
    }

    private string GetDiscoveryCandidateMatchPath(ITaskItem candidate)
    {
        var computedPath = StaticWebAsset.ComputeAssetRelativePath(candidate, out var property);
        if (property != null)
        {
            Log.LogMessage(
                MessageImportance.Low,
                "{0} '{1}' found for candidate '{2}' and will be used for matching.",
                property,
                computedPath,
                candidate.ItemSpec);
        }

        return computedPath;
    }

    private void ApplyGroupToAsset(
        ref StaticWebAsset asset,
        Dictionary<string, List<GroupDefinition>> definitionsBySourceId,
        StaticWebAssetGlobMatcher.MatchContext matchContext)
    {
        if (!definitionsBySourceId.TryGetValue(asset.SourceId, out var definitions))
        {
            return;
        }

        var result = MatchAssetToDefinitions(asset, definitions, matchContext);

        if (result.GroupEntries != null && result.GroupEntries.Count > 0)
        {
            asset.AssetGroups = string.Join(";", result.GroupEntries);
            asset.RelativePath = result.RelativePath;
            if (result.ContentRootSuffix != null)
            {
                ApplyContentRootSuffix(ref asset, result.ContentRootSuffix, result.ContentRootGroupName);
            }
        }
    }

    private readonly struct GroupMatchResult
    {
        public GroupMatchResult(
            List<string> groupEntries,
            string relativePath,
            string contentRootSuffix,
            string contentRootGroupName)
        {
            GroupEntries = groupEntries;
            RelativePath = relativePath;
            ContentRootSuffix = contentRootSuffix;
            ContentRootGroupName = contentRootGroupName;
        }

        public List<string> GroupEntries { get; }
        public string RelativePath { get; }
        public string ContentRootSuffix { get; }
        public string ContentRootGroupName { get; }
    }

    private readonly struct GroupDefinition
    {
        public GroupDefinition(
            string name,
            string value,
            string sourceId,
            int order,
            StaticWebAssetGlobMatcher includeMatcher,
            StaticWebAssetGlobMatcher excludeMatcher,
            StaticWebAssetGlobMatcher relativePathMatcher,
            string relativePathPrefix,
            string contentRootSuffix)
        {
            Name = name;
            Value = value;
            SourceId = sourceId;
            Order = order;
            IncludeMatcher = includeMatcher;
            ExcludeMatcher = excludeMatcher;
            RelativePathMatcher = relativePathMatcher;
            RelativePathPrefix = relativePathPrefix;
            ContentRootSuffix = contentRootSuffix;
        }

        public string Name { get; }
        public string Value { get; }
        public string SourceId { get; }
        public int Order { get; }
        public StaticWebAssetGlobMatcher IncludeMatcher { get; }
        public StaticWebAssetGlobMatcher ExcludeMatcher { get; }
        public StaticWebAssetGlobMatcher RelativePathMatcher { get; }
        public string RelativePathPrefix { get; }
        public string ContentRootSuffix { get; }
    }

    private Dictionary<string, List<GroupDefinition>> ParseGroupDefinitions()
    {
        var definitions = new List<GroupDefinition>();

        foreach (var def in StaticWebAssetGroupDefinitions)
        {
            var name = def.ItemSpec;
            var value = def.GetMetadata("Value");
            if (string.IsNullOrEmpty(value))
            {
                Log.LogError("Group definition '{0}' is missing required metadata 'Value'.", name);
                return null;
            }

            var sourceId = def.GetMetadata("SourceId");
            if (string.IsNullOrEmpty(sourceId))
            {
                Log.LogError("Group definition '{0}' is missing required metadata 'SourceId'.", name);
                return null;
            }

            var orderStr = def.GetMetadata("Order");
            if (!int.TryParse(orderStr, out var order))
            {
                Log.LogError("Group definition '{0}' has invalid or missing 'Order' value '{1}'. Order must be an integer.", name, orderStr);
                return null;
            }

            var includePattern = def.GetMetadata("IncludePattern");
            if (string.IsNullOrEmpty(includePattern))
            {
                Log.LogError("Group definition '{0}' is missing required metadata 'IncludePattern'.", name);
                return null;
            }
            var excludePattern = def.GetMetadata("ExcludePattern");
            var relativePathPattern = def.GetMetadata("RelativePathPattern");
            var relativePathPrefix = def.GetMetadata("RelativePathPrefix");
            var contentRootSuffix = def.GetMetadata("ContentRootSuffix");

            var includeMatcher = new StaticWebAssetGlobMatcherBuilder()
                .AddIncludePatterns(includePattern.Split(GroupPatternSeparator, StringSplitOptions.RemoveEmptyEntries))
                .Build();

            StaticWebAssetGlobMatcher excludeMatcher = null;
            if (!string.IsNullOrEmpty(excludePattern))
            {
                excludeMatcher = new StaticWebAssetGlobMatcherBuilder()
                    .AddIncludePatterns(excludePattern.Split(GroupPatternSeparator, StringSplitOptions.RemoveEmptyEntries))
                    .Build();
            }

            StaticWebAssetGlobMatcher relativePathMatcher = null;
            if (!string.IsNullOrEmpty(relativePathPattern))
            {
                relativePathMatcher = new StaticWebAssetGlobMatcherBuilder()
                    .AddIncludePatterns(relativePathPattern)
                    .Build();
            }

            definitions.Add(new GroupDefinition(name, value, sourceId, order, includeMatcher, excludeMatcher, relativePathMatcher, relativePathPrefix, contentRootSuffix));
        }

        // Validate that no two definitions share the same (Order, SourceId)
        for (var i = 0; i < definitions.Count; i++)
        {
            for (var j = i + 1; j < definitions.Count; j++)
            {
                if (definitions[i].Order == definitions[j].Order &&
                    string.Equals(definitions[i].SourceId, definitions[j].SourceId, StringComparison.Ordinal))
                {
                    Log.LogError(
                        "Group definitions '{0}' and '{1}' have the same Order ({2}) and SourceId ('{3}'). " +
                        "Each definition from the same source must have a unique Order to ensure deterministic evaluation.",
                        definitions[i].Name, definitions[j].Name, definitions[i].Order, definitions[i].SourceId);
                    return null;
                }
            }
        }

        definitions.Sort((a, b) => a.Order.CompareTo(b.Order));

        var result = new Dictionary<string, List<GroupDefinition>>(StringComparer.Ordinal);
        foreach (var def in definitions)
        {
            if (!result.TryGetValue(def.SourceId, out var list))
            {
                list = new List<GroupDefinition>();
                result[def.SourceId] = list;
            }
            list.Add(def);
        }
        return result;
    }

    private GroupMatchResult MatchAssetToDefinitions(
        StaticWebAsset asset, List<GroupDefinition> definitions, StaticWebAssetGlobMatcher.MatchContext matchContext)
    {
        var currentRelativePath = asset.RelativePath;
        var pathWithoutTokens = StaticWebAssetPathPattern.PathWithoutTokens(currentRelativePath);
        var groupEntries = new List<string>();
        var groupValues = new Dictionary<string, string>(StringComparer.Ordinal);
        string contentRootSuffix = null;
        string contentRootGroupName = null;

        foreach (var def in definitions)
        {
            matchContext.SetPathAndReinitialize(pathWithoutTokens);
            var includeMatch = def.IncludeMatcher.Match(matchContext);

            if (!includeMatch.IsMatch)
            {
                continue;
            }

            if (def.ExcludeMatcher != null)
            {
                matchContext.SetPathAndReinitialize(pathWithoutTokens);
                var excludeMatch = def.ExcludeMatcher.Match(matchContext);
                if (excludeMatch.IsMatch)
                {
                    Log.LogMessage(MessageImportance.Low, "Asset '{0}' excluded from group '{1}={2}' by ExcludePattern.", asset.Identity, def.Name, def.Value);
                    continue;
                }
            }

            if (groupValues.TryGetValue(def.Name, out var existingValue))
            {
                if (!string.Equals(existingValue, def.Value, StringComparison.Ordinal))
                {
                    Log.LogError("Asset '{0}' matched group definitions for '{1}' with conflicting values '{2}' and '{3}'. Glob patterns must be non-overlapping for the same group name with different values.",
                        asset.Identity, def.Name, existingValue, def.Value);
                    return default;
                }
                continue;
            }

            groupValues.Add(def.Name, def.Value);
            groupEntries.Add(def.Name + "=" + def.Value);
            Log.LogMessage(MessageImportance.Low, "Tagged asset '{0}' with group '{1}={2}'.", asset.Identity, def.Name, def.Value);

            if (def.RelativePathMatcher != null)
            {
                matchContext.SetPathAndReinitialize(pathWithoutTokens);
                var rpMatch = def.RelativePathMatcher.Match(matchContext);
                if (rpMatch.IsMatch)
                {
                    // Safe to use pathWithoutTokens here: DefineStaticWebAssets runs on raw
                    // Content items before fingerprint/token expressions are applied.
                    var newRelativePath = StaticWebAsset.Normalize(rpMatch.Stem);

                    if (!string.IsNullOrEmpty(def.RelativePathPrefix))
                    {
                        newRelativePath = def.RelativePathPrefix + newRelativePath;
                        Log.LogMessage(MessageImportance.Low, "Group '{0}' prepended RelativePathPrefix '{1}' to relative path.", def.Name, def.RelativePathPrefix);
                    }

                    Log.LogMessage(MessageImportance.Low, "Group '{0}' transformed RelativePath from '{1}' to '{2}'.",
                        def.Name, currentRelativePath, newRelativePath);

                    currentRelativePath = newRelativePath;
                    pathWithoutTokens = StaticWebAssetPathPattern.PathWithoutTokens(currentRelativePath);
                }
            }

            if (!string.IsNullOrEmpty(def.RelativePathPrefix) && def.RelativePathMatcher == null)
            {
                currentRelativePath = def.RelativePathPrefix + currentRelativePath;
                pathWithoutTokens = StaticWebAssetPathPattern.PathWithoutTokens(currentRelativePath);
                Log.LogMessage(MessageImportance.Low, "Group '{0}' prepended RelativePathPrefix '{1}' to relative path.", def.Name, def.RelativePathPrefix);
            }

            if (!string.IsNullOrEmpty(def.ContentRootSuffix))
            {
                // Content root suffixes compose: version=v4 (suffix "v4") + theme=light (suffix "light")
                // produces "OriginalRoot/v4/light". Definitions are sorted by Order so composition
                // is deterministic.
                if (contentRootSuffix != null)
                {
                    contentRootSuffix = contentRootSuffix.TrimEnd('/', '\\') + "/" + def.ContentRootSuffix.TrimStart('/', '\\');
                }
                else
                {
                    contentRootSuffix = def.ContentRootSuffix;
                }
                contentRootGroupName = def.Name;
            }
        }

        return new GroupMatchResult(groupEntries, currentRelativePath, contentRootSuffix, contentRootGroupName);
    }

    private void ApplyContentRootSuffix(ref StaticWebAsset asset, string contentRootSuffix, string groupName)
    {
        var normalizedContentRoot = asset.ContentRoot.TrimEnd('/', '\\');
        var normalizedSuffix = contentRootSuffix.Trim('/', '\\');
        asset.ContentRoot = StaticWebAsset.NormalizeContentRootPath(normalizedContentRoot + "/" + normalizedSuffix);
        Log.LogMessage(MessageImportance.Low,
            "Group '{0}' adjusted ContentRoot to '{1}' via ContentRootSuffix.",
            groupName, asset.ContentRoot);
    }
}
