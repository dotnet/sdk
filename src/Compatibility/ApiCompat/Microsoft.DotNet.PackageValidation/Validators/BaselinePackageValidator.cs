// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiCompatibility.Runner;
using NuGet.ContentModel;
using NuGet.Frameworks;

namespace Microsoft.DotNet.PackageValidation.Validators
{
    /// <summary>
    /// Validates that no target framework / rid support is dropped in the latest package.
    /// Reports all the breaking changes in the latest package.
    /// </summary>
    public sealed class BaselinePackageValidator(ISuppressibleLog log,
        IApiCompatRunner apiCompatRunner) : IPackageValidator
    {
        /// <summary>
        /// Validates the latest NuGet package doesn't drop any target framework/rid and does not introduce any breaking changes.
        /// </summary>
        /// <param name="options"><see cref="PackageValidatorOption"/> to configure the baseline package validation.</param>
        public void Validate(PackageValidatorOption options)
        {
            if (options.BaselinePackage is null)
            {
                throw new ArgumentNullException(nameof(options.BaselinePackage));
            }

            ApiCompatRunnerOptions apiCompatOptions = new(options.EnableStrictMode, isBaselineComparison: true);

            foreach (NuGetFramework baselineTargetFramework in options.BaselinePackage.FrameworksInPackage)
            {
                // Skip target frameworks excluded from the baseline package.
                if (options.BaselinePackageFrameworkFilter is not null &&
                   options.BaselinePackageFrameworkFilter.IsExcluded(baselineTargetFramework))
                {
                    continue;
                }

                // Retrieve the compile time assets from the baseline package
                IReadOnlyList<ContentItem>? baselineCompileAssets = options.BaselinePackage.FindBestCompileAssetForFramework(baselineTargetFramework);
                if (baselineCompileAssets != null)
                {
                    // Search for compatible compile time assets in the latest package.
                    IReadOnlyList<ContentItem>? latestCompileAssets = options.Package.FindBestCompileAssetForFramework(baselineTargetFramework);
                    if (latestCompileAssets == null)
                    {
                        log.LogError(new Suppression(DiagnosticIds.TargetFrameworkDropped,
                            string.Format(Resources.MissingTargetFramework, baselineTargetFramework),
                            baselineTargetFramework.ToString()));
                    }
                    else if (options.EnqueueApiCompatWorkItems)
                    {
                        apiCompatRunner.QueueApiCompatFromContentItem(log,
                            baselineCompileAssets,
                            latestCompileAssets,
                            apiCompatOptions,
                            options.BaselinePackage,
                            options.Package);
                    }
                }

                // Retrieve runtime baseline assets and searches for compatible runtime assets in the latest package.
                IReadOnlyList<ContentItem>? baselineRuntimeAssets = options.BaselinePackage.FindBestRuntimeAssetForFramework(baselineTargetFramework);
                if (baselineRuntimeAssets != null)
                {
                    // Search for compatible runtime assets in the latest package.
                    IReadOnlyList<ContentItem>? latestRuntimeAssets = options.Package.FindBestRuntimeAssetForFramework(baselineTargetFramework);
                    if (latestRuntimeAssets == null)
                    {
                        log.LogError(new Suppression(DiagnosticIds.TargetFrameworkDropped,
                            string.Format(Resources.MissingTargetFramework, baselineTargetFramework),
                            baselineTargetFramework.ToString()));
                    }
                    else if (options.EnqueueApiCompatWorkItems)
                    {
                        apiCompatRunner.QueueApiCompatFromContentItem(log,
                            baselineRuntimeAssets,
                            latestRuntimeAssets,
                            apiCompatOptions,
                            options.BaselinePackage,
                            options.Package);
                    }
                }

                // Retrieve runtime specific baseline assets and searches for compatible runtime specific assets in the latest package.
                IReadOnlyList<ContentItem>? baselineRuntimeSpecificAssets = options.BaselinePackage.FindBestRuntimeSpecificAssetForFramework(baselineTargetFramework);
                if (baselineRuntimeSpecificAssets != null && baselineRuntimeSpecificAssets.Count > 0)
                {
                    IEnumerable<IGrouping<string, ContentItem>> baselineRuntimeSpecificAssetsRidGroupedPerRid = baselineRuntimeSpecificAssets
                        .Where(t => t.Path.StartsWith("runtimes"))
                        .GroupBy(t => (string)t.Properties["rid"]);

                    foreach (IGrouping<string, ContentItem> baselineRuntimeSpecificAssetsRidGroup in baselineRuntimeSpecificAssetsRidGroupedPerRid)
                    {
                        IReadOnlyList<ContentItem>? latestRuntimeSpecificAssets = options.Package.FindBestRuntimeAssetForFrameworkAndRuntime(baselineTargetFramework, baselineRuntimeSpecificAssetsRidGroup.Key);
                        if (latestRuntimeSpecificAssets == null)
                        {
                            log.LogError(new Suppression(DiagnosticIds.TargetFrameworkAndRidPairDropped,
                                string.Format(Resources.MissingTargetFrameworkAndRid,
                                    baselineTargetFramework,
                                    baselineRuntimeSpecificAssetsRidGroup.Key),
                                baselineTargetFramework.ToString() + "-" + baselineRuntimeSpecificAssetsRidGroup.Key));
                        }
                        else if (options.EnqueueApiCompatWorkItems)
                        {
                            apiCompatRunner.QueueApiCompatFromContentItem(log,
                                baselineRuntimeSpecificAssetsRidGroup.ToArray(),
                                latestRuntimeSpecificAssets,
                                apiCompatOptions,
                                options.BaselinePackage,
                                options.Package);
                        }
                    }
                }
            }

            // If baseline target frameworks are excluded, provide additional logging.
            if (options.BaselinePackageFrameworkFilter is not null &&
                options.BaselinePackageFrameworkFilter.FoundExcludedTargetFrameworks.Count > 0)
            {
                log.LogMessage(ApiSymbolExtensions.Logging.MessageImportance.Normal,
                    string.Format(Resources.BaselineTargetFrameworksIgnored,
                        string.Join(", ", options.BaselinePackageFrameworkFilter.FoundExcludedTargetFrameworks)));

                // If a baseline target framework is ignored but present in the current package, emit a warning.
                // This should help avoiding unintentional exclusions when using wildcards in the exclusion patterns.
                string[] baselineTargetFrameworksExcludedButPresentInCurrentPackage = options.Package.FrameworksInPackage
                    .Select(framework => framework.GetShortFolderName())
                    .Intersect(options.BaselinePackageFrameworkFilter.FoundExcludedTargetFrameworks)
                    .ToArray();
                foreach (string baselineTargetFrameworkExcludedButPresentInCurrentPackage in baselineTargetFrameworksExcludedButPresentInCurrentPackage)
                {
                    log.LogWarning(new Suppression(DiagnosticIds.BaselineTargetFrameworkIgnoredButPresentInCurrentPackage,
                        string.Format(Resources.BaselineTargetFrameworkIgnoredButPresentInCurrentPackage, baselineTargetFrameworkExcludedButPresentInCurrentPackage),
                        baselineTargetFrameworkExcludedButPresentInCurrentPackage));
                }
            }

            if (options.ExecuteApiCompatWorkItems)
            {
                apiCompatRunner.ExecuteWorkItems();
            }
        }
    }
}
