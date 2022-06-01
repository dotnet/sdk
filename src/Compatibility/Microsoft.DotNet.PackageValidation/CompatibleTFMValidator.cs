// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ApiCompatibility.Logging;
using NuGet.ContentModel;
using NuGet.Frameworks;

namespace Microsoft.DotNet.PackageValidation
{
    /// <summary>
    /// Validates that there are compile time and runtime assets for all the compatible frameworks.
    /// Queues the apicompat between the applicable compile and runtime assemblies for these frameworks.
    /// </summary>
    public class CompatibleTfmValidator
    {
        private static readonly Dictionary<NuGetFramework, HashSet<NuGetFramework>> s_packageTfmMapping = InitializeTfmMappings();
        private readonly bool _runApiCompat;
        private readonly ApiCompatRunner _apiCompatRunner;
        private readonly CompatibilityLoggerBase _log;

        public CompatibleTfmValidator(bool runApiCompat, bool enableStrictMode, CompatibilityLoggerBase log, Dictionary<string, HashSet<string>> apiCompatReferences)
        {
            _runApiCompat = runApiCompat;
            _log = log;
            _apiCompatRunner = new(enableStrictMode, _log, apiCompatReferences);
        }

        /// <summary>
        /// Validates that there are compile time and runtime assets for all the compatible frameworks.
        /// Validates that the surface between compile time and runtime assets is compatible.
        /// </summary>
        /// <param name="package">Nuget Package that needs to be validated.</param>
        public void Validate(Package package)
        {
            HashSet<NuGetFramework> compatibleTargetFrameworks = new();
            foreach (NuGetFramework item in package.FrameworksInPackage)
            {
                compatibleTargetFrameworks.Add(item);
                if (s_packageTfmMapping.ContainsKey(item))
                {
                    compatibleTargetFrameworks.UnionWith(s_packageTfmMapping[item]);
                }
            }

            foreach (NuGetFramework framework in compatibleTargetFrameworks)
            {
                ContentItem? compileTimeAsset = package.FindBestCompileAssetForFramework(framework);
                if (compileTimeAsset == null)
                {
                    _log.LogError(
                        new Suppression(DiagnosticIds.ApplicableCompileTimeAsset) { Target = framework.ToString() },
                        DiagnosticIds.ApplicableCompileTimeAsset,
                        Resources.NoCompatibleCompileTimeAsset,
                        framework.ToString());
                    break;
                }

                ContentItem? runtimeAsset = package.FindBestRuntimeAssetForFramework(framework);
                if (runtimeAsset == null)
                {
                    _log.LogError(
                        new Suppression(DiagnosticIds.CompatibleRuntimeRidLessAsset) { Target = framework.ToString() },
                        DiagnosticIds.CompatibleRuntimeRidLessAsset,
                        Resources.NoCompatibleRuntimeAsset,
                        framework.ToString());
                }
                // Invoke ApiCompat to compare the compile time asset with the runtime asset if they are not the same assembly.
                else if (_runApiCompat && compileTimeAsset.Path != runtimeAsset.Path)
                {
                    string header = string.Format(Resources.ApiCompatibilityHeader, compileTimeAsset.Path, runtimeAsset.Path);
                    _apiCompatRunner.QueueApiCompatFromContentItem(package.PackageId, compileTimeAsset, runtimeAsset, header);
                }

                foreach (string rid in package.Rids.Where(t => IsSupportedRidTargetFrameworkPair(framework, t)))
                {
                    ContentItem? runtimeRidSpecificAsset = package.FindBestRuntimeAssetForFrameworkAndRuntime(framework, rid);
                    if (runtimeRidSpecificAsset == null)
                    {
                        _log.LogError(
                            new Suppression(DiagnosticIds.CompatibleRuntimeRidSpecificAsset) { Target = framework.ToString() + "-" + rid },
                            DiagnosticIds.CompatibleRuntimeRidSpecificAsset,
                            Resources.NoCompatibleRidSpecificRuntimeAsset,
                            framework.ToString(),
                            rid);
                    }
                    // Invoke ApiCompat to compare the compile time asset with the runtime specific asset if they are not the same and
                    // if the comparison hasn't already happened (when the runtime asset is the same as the runtime specific asset).
                    else if (_runApiCompat &&
                        compileTimeAsset.Path != runtimeRidSpecificAsset.Path &&
                        (runtimeAsset == null || runtimeAsset.Path != runtimeRidSpecificAsset.Path))
                    {
                        string header = string.Format(Resources.ApiCompatibilityHeader, compileTimeAsset.Path, runtimeRidSpecificAsset.Path);
                        _apiCompatRunner.QueueApiCompatFromContentItem(package.PackageId, compileTimeAsset, runtimeRidSpecificAsset, header);
                    }
                }
            }

            if (_runApiCompat)
                _apiCompatRunner.RunApiCompat(package.PackagePath, package.PackagePath);
        }

        private static Dictionary<NuGetFramework, HashSet<NuGetFramework>> InitializeTfmMappings()
        {
            Dictionary<NuGetFramework, HashSet<NuGetFramework>> packageTfmMapping = new();

            // creating a map framework in package => frameworks to test based on default compatibilty mapping.
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
                    packageTfmMapping.Add(forwardTfm, new HashSet<NuGetFramework> { reverseTfm });
                }
            }

            return packageTfmMapping;
        }

        private static bool IsSupportedRidTargetFrameworkPair(NuGetFramework tfm, string rid) =>
            tfm.Framework != ".NETFramework" || rid.StartsWith("win", StringComparison.OrdinalIgnoreCase);
    }
}
