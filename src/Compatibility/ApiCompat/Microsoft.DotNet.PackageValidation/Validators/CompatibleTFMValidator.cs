// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiCompatibility.Runner;
using NuGet.ContentModel;
using NuGet.Frameworks;

namespace Microsoft.DotNet.PackageValidation.Validators
{
    /// <summary>
    /// Validates that there are compile time and runtime assets for all the compatible frameworks.
    /// Queues APICompat work items for the applicable compile and runtime assemblies for these frameworks.
    /// </summary>
    public class CompatibleTfmValidator(ISuppressibleLog log,
        IApiCompatRunner apiCompatRunner) : IPackageValidator
    {
        private static readonly Dictionary<NuGetFramework, HashSet<NuGetFramework>> s_packageTfmMapping = InitializeTfmMappings();

        /// <summary>
        /// Validates that there are compile time and runtime assets for all the compatible frameworks.
        /// Validates that the surface between compile time and runtime assets is compatible.
        /// </summary>
        /// <param name="options"><see cref="PackageValidatorOption"/> to configure the compatible TFM package validation.</param>
        public void Validate(PackageValidatorOption options)
        {
            ApiCompatRunnerOptions apiCompatOptions = new(options.EnableStrictMode);

            HashSet<NuGetFramework> compatibleTargetFrameworks = [];
            foreach (NuGetFramework item in options.Package.FrameworksInPackage)
            {
                compatibleTargetFrameworks.Add(item);
                if (s_packageTfmMapping.ContainsKey(item))
                {
                    compatibleTargetFrameworks.UnionWith(s_packageTfmMapping[item]);
                }
            }

            foreach (NuGetFramework framework in compatibleTargetFrameworks)
            {
                IReadOnlyList<ContentItem>? compileTimeAsset = options.Package.FindBestCompileAssetForFramework(framework);
                if (compileTimeAsset == null)
                {
                    log.LogError(new Suppression(DiagnosticIds.ApplicableCompileTimeAsset) { Target = framework.ToString() },
                        DiagnosticIds.ApplicableCompileTimeAsset,
                        string.Format(Resources.NoCompatibleCompileTimeAsset,
                            framework));
                    continue;
                }

                IReadOnlyList<ContentItem>? runtimeAsset = options.Package.FindBestRuntimeAssetForFramework(framework);
                // Emit an error if
                // - No runtime asset is available or
                // - The runtime asset is a placeholder but the compile time asset isn't.
                if (runtimeAsset == null ||
                    (runtimeAsset.IsPlaceholderFile() && !compileTimeAsset.IsPlaceholderFile()))
                {
                    log.LogError(new Suppression(DiagnosticIds.CompatibleRuntimeRidLessAsset) { Target = framework.ToString() },
                        DiagnosticIds.CompatibleRuntimeRidLessAsset,
                        string.Format(Resources.NoCompatibleRuntimeAsset,
                            framework));
                }
                // Ignore the additional runtime asset when performing in non-strict mode, otherwise emit a missing
                // compile time asset error.
                else if (compileTimeAsset.IsPlaceholderFile() && !runtimeAsset.IsPlaceholderFile())
                {
                    if (options.EnableStrictMode)
                    {
                        log.LogError(new Suppression(DiagnosticIds.ApplicableCompileTimeAsset, framework.ToString()),
                            DiagnosticIds.ApplicableCompileTimeAsset,
                            string.Format(Resources.NoCompatibleCompileTimeAsset, framework.ToString()));
                    }
                }
                // Invoke ApiCompat to compare the compile time asset with the runtime asset.
                else if (options.EnqueueApiCompatWorkItems)
                {
                    apiCompatRunner.QueueApiCompatFromContentItem(log,
                        compileTimeAsset,
                        runtimeAsset,
                        apiCompatOptions,
                        options.Package);
                }

                foreach (string rid in options.Package.Rids.Where(packageRid => framework.SupportsRuntimeIdentifier(packageRid)))
                {
                    IReadOnlyList<ContentItem>? runtimeRidSpecificAsset = options.Package.FindBestRuntimeAssetForFrameworkAndRuntime(framework, rid);
                    // Emit an error if
                    // - No runtime specific asset is available or
                    // - The runtime specific asset is a placeholder but the compile time asset isn't.
                    if (runtimeRidSpecificAsset == null ||
                        (runtimeRidSpecificAsset.IsPlaceholderFile() && !compileTimeAsset.IsPlaceholderFile()))
                    {
                        log.LogError(new Suppression(DiagnosticIds.CompatibleRuntimeRidSpecificAsset) { Target = framework.ToString() + "-" + rid },
                            DiagnosticIds.CompatibleRuntimeRidSpecificAsset,
                            string.Format(Resources.NoCompatibleRidSpecificRuntimeAsset,
                                framework,
                                rid));
                    }
                    // Ignore the additional runtime specific asset when performing in non-strict mode, otherwise emit a
                    // missing compile time asset error.
                    else if (compileTimeAsset.IsPlaceholderFile() && !runtimeRidSpecificAsset.IsPlaceholderFile())
                    {
                        if (options.EnableStrictMode)
                        {
                            log.LogError(new Suppression(DiagnosticIds.ApplicableCompileTimeAsset, framework.ToString()),
                                DiagnosticIds.ApplicableCompileTimeAsset,
                                string.Format(Resources.NoCompatibleCompileTimeAsset, framework.ToString()));
                        }
                    }
                    // Invoke ApiCompat to compare the compile asset with the runtime specific asset.
                    else if (options.EnqueueApiCompatWorkItems)
                    {
                        apiCompatRunner.QueueApiCompatFromContentItem(log,
                            compileTimeAsset,
                            runtimeRidSpecificAsset,
                            apiCompatOptions,
                            options.Package);
                    }
                }
            }

            if (options.ExecuteApiCompatWorkItems)
            {
                apiCompatRunner.ExecuteWorkItems();
            }
        }

        private static Dictionary<NuGetFramework, HashSet<NuGetFramework>> InitializeTfmMappings()
        {
            Dictionary<NuGetFramework, HashSet<NuGetFramework>> packageTfmMapping = [];

            // creating a map framework in package => frameworks to test based on default compatibility mapping.
            foreach (OneWayCompatibilityMappingEntry item in DefaultFrameworkMappings.Instance.CompatibilityMappings)
            {
                NuGetFramework forwardTfm = item.SupportedFrameworkRange.Max;
                NuGetFramework reverseTfm = item.TargetFrameworkRange.Min;
                if (packageTfmMapping.ContainsKey(forwardTfm))
                {
                    packageTfmMapping[forwardTfm].Add(reverseTfm);
                }
                else
                {
                    packageTfmMapping.Add(forwardTfm, [ reverseTfm ]);
                }
            }

            return packageTfmMapping;
        }
    }
}
