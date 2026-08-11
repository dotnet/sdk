// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Net.Http.Headers;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Internal;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
{
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
    public class DefineStaticWebAssets : Task
    {
        private const string DefaultFingerprintExpression = "#[.{fingerprint}]?";

        [Required]
        public ITaskItem[] CandidateAssets { get; set; }

        public ITaskItem[] PropertyOverrides { get; set; }

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

        [Output]
        public ITaskItem[] Assets { get; set; }

        [Output]
        public ITaskItem[] CopyCandidates { get; set; }

        public override bool Execute()
        {
            try
            {
                var results = new List<ITaskItem>();
                var copyCandidates = new List<ITaskItem>();

                var matcher = !string.IsNullOrEmpty(RelativePathPattern) ? new Matcher().AddInclude(RelativePathPattern) : null;
                var filter = !string.IsNullOrEmpty(RelativePathFilter) ? new Matcher().AddInclude(RelativePathFilter) : null;
                var assetsByRelativePath = new Dictionary<string, List<ITaskItem>>();
                var fingerprintPatterns = (FingerprintPatterns ?? []).Select(p => new FingerprintPattern(p)).ToArray();
                var tokensByPattern = fingerprintPatterns.Where(p => !string.IsNullOrEmpty(p.Expression)).ToDictionary(p => p.Pattern.Substring(1), p => p.Expression);
                Array.Sort(fingerprintPatterns, (a, b) => a.Pattern.Count(c => c == '.').CompareTo(b.Pattern.Count(c => c == '.')));

                for (var i = 0; i < CandidateAssets.Length; i++)
                {
                    var candidate = CandidateAssets[i];
                    var relativePathCandidate = string.Empty;
                    if (SourceType == StaticWebAsset.SourceTypes.Discovered)
                    {
                        var candidateMatchPath = GetDiscoveryCandidateMatchPath(candidate);
                        relativePathCandidate = candidateMatchPath;
                        if (matcher != null && string.IsNullOrEmpty(candidate.GetMetadata("RelativePath")))
                        {
                            var match = matcher.Match(StaticWebAssetPathPattern.PathWithoutTokens(candidateMatchPath));
                            if (!match.HasMatches)
                            {
                                Log.LogMessage(MessageImportance.Low, "Rejected asset '{0}' for pattern '{1}'", candidateMatchPath, RelativePathPattern);
                                continue;
                            }

                            Log.LogMessage(MessageImportance.Low, "Accepted asset '{0}' for pattern '{1}' with relative path '{2}'", candidateMatchPath, RelativePathPattern, match.Files.Single().Stem);

                            relativePathCandidate = StaticWebAsset.Normalize(match.Files.Single().Stem);
                        }
                    }
                    else
                    {
                        relativePathCandidate = GetCandidateMatchPath(candidate);
                        if (matcher != null)
                        {
                            var match = matcher.Match(StaticWebAssetPathPattern.PathWithoutTokens(relativePathCandidate));
                            if (match.HasMatches)
                            {
                                var newRelativePathCandidate = match.Files.Single().Stem;
                                Log.LogMessage(
                                    MessageImportance.Low,
                                    "The relative path '{0}' matched the pattern '{1}'. Replacing relative path with '{2}'.",
                                    relativePathCandidate,
                                    RelativePathPattern,
                                    newRelativePathCandidate);

                                relativePathCandidate = newRelativePathCandidate;
                            }
                        }

                        if (filter != null && !filter.Match(StaticWebAssetPathPattern.PathWithoutTokens(relativePathCandidate)).HasMatches)
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
                    switch ((fingerprint, integrity))
                    {
                        case (null, null):
                            Log.LogMessage(MessageImportance.Low, "Computing fingerprint and integrity for asset '{0}'", candidate.ItemSpec);
                            (fingerprint, integrity) = (StaticWebAsset.ComputeFingerprintAndIntegrity(candidate.ItemSpec, originalItemSpec));
                            break;
                        case (null, not null):
                            Log.LogMessage(MessageImportance.Low, "Computing fingerprint for asset '{0}'", candidate.ItemSpec);
                            fingerprint = FileHasher.ToBase36(Convert.FromBase64String(integrity));
                            break;
                        case (not null, null):
                            Log.LogMessage(MessageImportance.Low, "Computing integrity for asset '{0}'", candidate.ItemSpec);
                            integrity = StaticWebAsset.ComputeIntegrity(candidate.ItemSpec, originalItemSpec);
                            break;
                    }

                    // If we are not able to compute the value based on an existing value or a default, we produce an error and stop.
                    if (Log.HasLoggedErrors)
                    {
                        break;
                    }

                    var identity = Path.GetFullPath(candidate.GetMetadata("FullPath"));
                    if (!string.Equals(SourceType, StaticWebAsset.SourceTypes.Discovered, StringComparison.OrdinalIgnoreCase))
                    {
                        // We ignore the content root for publish only assets since it doesn't matter.
                        var contentRootPrefix = StaticWebAsset.AssetKinds.IsPublish(assetKind) ? null : contentRoot;
                        (identity, var computed) = ComputeCandidateIdentity(candidate, contentRootPrefix, relativePathCandidate, matcher);

                        if (computed)
                        {
                            copyCandidates.Add(new TaskItem(candidate.ItemSpec, new Dictionary<string, string>
                            {
                                ["TargetPath"] = identity
                            }));
                        }
                    }

                    relativePathCandidate = FingerprintCandidates ?
                        StaticWebAsset.Normalize(AppendFingerprintPattern(relativePathCandidate, identity, fingerprintPatterns, tokensByPattern)) :
                        relativePathCandidate;

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
                        originalItemSpec);

                    asset.Normalize();
                    var item = asset.ToTaskItem();
                    if (SourceType == StaticWebAsset.SourceTypes.Discovered)
                    {
                        item.SetMetadata(nameof(StaticWebAsset.AssetKind), !asset.ShouldCopyToPublishDirectory() ? StaticWebAsset.AssetKinds.Build : StaticWebAsset.AssetKinds.All);
                        UpdateAssetKindIfNecessary(assetsByRelativePath, asset.RelativePath, item);
                    }

                    results.Add(item);
                }

                Assets = [.. results];
                CopyCandidates = [.. copyCandidates];
            }
            catch (Exception ex)
            {
                Log.LogError(ex.ToString());
            }

            return !Log.HasLoggedErrors;
        }

        private string AppendFingerprintPattern(
            string relativePathCandidate,
            string identity,
            FingerprintPattern[] fingerprintPatterns,
            IDictionary<string, string> tokensByPattern)
        {
            if (relativePathCandidate.Contains("#["))
            {
                var pattern = StaticWebAssetPathPattern.Parse(relativePathCandidate, identity);
                foreach (var segment in pattern.Segments)
                {
                    foreach (var part in segment.Parts)
                    {
                        foreach (var name in segment.GetTokenNames())
                        {
                            if (string.Equals(name, "fingerprint", StringComparison.OrdinalIgnoreCase))
                            {
                                return relativePathCandidate;
                            }
                        }
                    }
                }
            }

            // Fingerprinting patterns for content.By default(most common case), we check for a single extension, like.js or.css.
            // In that situation we apply the fingerprint expression directly to the file name, like app.js->app#[.{fingerprint}].js.
            // If we detect more than one extension, for example, Rcl.lib.module.js or Rcl.Razor.js, we retrieve the last extension and
            // check for a mapping in the list below.If we find a match, we apply the fingerprint expression to the file name, like
            // Rcl.lib.module.js->Rcl#[.{fingerprint}].lib.module.js. If we don't find a match, we add the extension to the name and
            // continue matching against the next segment, like Rcl.Razor.js->Rcl.Razor#[.{fingerprint}].js.
            // If we don't find a match, we apply the fingerprint before the first extension, like Rcl.Razor.js -> Rcl.Razor#[.{fingerprint}].js.
            var directoryName = Path.GetDirectoryName(relativePathCandidate);
            relativePathCandidate = Path.GetFileName(relativePathCandidate);
            var extensionCount = 0;
            var stem = relativePathCandidate;
            var extension = Path.GetExtension(relativePathCandidate);
            while (!string.IsNullOrEmpty(extension) || extensionCount < 2)
            {
                extensionCount++;
                stem = stem.Substring(0, stem.Length - extension.Length);
                extension = Path.GetExtension(stem);
            }

            // Simple case, single extension or no extension
            // For example:
            // app.js->app#[.{fingerprint}]?.js
            // app->README#[.{fingerprint}]?
            if (extensionCount < 2)
            {
                if (!tokensByPattern.TryGetValue(extension, out var expression))
                {
                    expression = DefaultFingerprintExpression;
                }

                var simpleExtensionResult = Path.Combine(directoryName, $"{stem}{expression}{extension}");
                Log.LogMessage(MessageImportance.Low, "Fingerprinting asset '{0}' as '{1}'", relativePathCandidate, simpleExtensionResult);
                return simpleExtensionResult;
            }

            // Complex case, multiple extensions, try matching against known patterns
            // For example:
            // Rcl.lib.module.js->Rcl#[.{fingerprint}].lib.module.js
            // Rcl.Razor.js->Rcl.Razor#[.{fingerprint}].js
            foreach (var pattern in fingerprintPatterns)
            {
                var matchResult = pattern.Matcher.Match(StaticWebAssetPathPattern.PathWithoutTokens(relativePathCandidate));
                if (matchResult.HasMatches)
                {
                    stem = relativePathCandidate.Substring(0, (1 + relativePathCandidate.Length - pattern.Pattern.Length));
                    extension = relativePathCandidate.Substring(stem.Length);
                    if (!tokensByPattern.TryGetValue(extension, out var expression))
                    {
                        expression = DefaultFingerprintExpression;
                    }
                    var patternResult = Path.Combine(directoryName, $"{stem}{expression}{extension}");
                    Log.LogMessage(MessageImportance.Low, "Fingerprinting asset '{0}' as '{1}' because it matched pattern '{2}'", relativePathCandidate, patternResult, pattern.Pattern);
                    return patternResult;
                }
            }

            // Multiple extensions and no match, apply the fingerprint before the first extension
            // For example:
            // Rcl.Razor.js->Rcl.Razor#[.{fingerprint}].js
            stem = Path.GetFileNameWithoutExtension(relativePathCandidate);
            extension = Path.GetExtension(relativePathCandidate);
            var result = Path.Combine(directoryName, $"{stem}{DefaultFingerprintExpression}{extension}");
            Log.LogMessage(MessageImportance.Low, "Fingerprinting asset '{0}' as '{1}' because it didn't match any pattern", relativePathCandidate, result);

            return result;
        }

        private (string identity, bool computed) ComputeCandidateIdentity(ITaskItem candidate, string contentRoot, string relativePath, Matcher matcher)
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

                var matchResult = matcher?.Match(StaticWebAssetPathPattern.PathWithoutTokens(candidate.ItemSpec));
                if (matcher == null)
                {
                    // If no relative path pattern was specified, we are going to suggest that the identity is `%(ContentRoot)\RelativePath\OriginalFileName`
                    // We don't want to use the relative path file name since multiple assets might map to that and conflicts might arise.
                    // Alternatively, we could be explicit here and support ContentRootSubPath to indicate where it needs to go.
                    var identitySubPath = Path.GetDirectoryName(relativePath);
                    var itemSpecFileName = Path.GetFileName(candidateFullPath);
                    var finalIdentity = Path.Combine(normalizedContentRoot, identitySubPath, itemSpecFileName);
                    Log.LogMessage(MessageImportance.Low, "Identity for candidate '{0}' is '{1}' because it did not start with the content root '{2}'", candidate.ItemSpec, finalIdentity, normalizedContentRoot);
                    return (finalIdentity, true);
                }
                else if (!matchResult.HasMatches)
                {
                    Log.LogMessage(MessageImportance.Low, "Identity for candidate '{0}' is '{1}' because it didn't match the relative path pattern", candidate.ItemSpec, candidateFullPath);
                    return (candidateFullPath, false);
                }
                else
                {
                    var stem = matchResult.Files.Single().Stem;
                    var assetIdentity = Path.GetFullPath(Path.Combine(normalizedContentRoot, stem));
                    Log.LogMessage(MessageImportance.Low, "Computed identity '{0}' for candidate '{1}'", assetIdentity, candidate.ItemSpec);

                    return (assetIdentity, true);
                }
            }
        }

        private string ComputePropertyValue(ITaskItem element, string metadataName, string propertyValue, bool isRequired = true)
        {
            if (PropertyOverrides != null && PropertyOverrides.Any(a => string.Equals(a.ItemSpec, metadataName, StringComparison.OrdinalIgnoreCase)))
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

        private void UpdateAssetKindIfNecessary(Dictionary<string, List<ITaskItem>> assetsByRelativePath, string candidateRelativePath, ITaskItem asset)
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
                assetsByRelativePath.Add(candidateRelativePath, [asset]);
            }
            else
            {
                if (existing.Count == 2)
                {
                    var first = existing[0];
                    var second = existing[1];
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
                else if (existing.Count == 1)
                {
                    var existingAsset = existing[0];
                    switch ((asset.GetMetadata(nameof(StaticWebAsset.CopyToPublishDirectory)), existingAsset.GetMetadata(nameof(StaticWebAsset.CopyToPublishDirectory))))
                    {
                        case (StaticWebAsset.AssetCopyOptions.Never, StaticWebAsset.AssetCopyOptions.Never):
                        case (not StaticWebAsset.AssetCopyOptions.Never, not StaticWebAsset.AssetCopyOptions.Never):
                            var errorMessage = "Two assets found targeting the same path with incompatible asset kinds: " + Environment.NewLine +
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
                            existing.Add(asset);
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
                            existing.Add(asset);
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

        private class FingerprintPattern(ITaskItem pattern)
        {
            Matcher _matcher;
            public string Name { get; set; } = pattern.ItemSpec;

            public string Pattern { get; set; } = pattern.GetMetadata(nameof(Pattern));

            public string Expression { get; set; } = pattern.GetMetadata(nameof(Expression));

            public Matcher Matcher => _matcher ??= new Matcher().AddInclude(Pattern);
        }
    }
}
