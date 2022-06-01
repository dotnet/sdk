// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.DotNet.ApiCompatibility.Logging;
using NuGet.ContentModel;
using NuGet.Frameworks;

namespace Microsoft.DotNet.PackageValidation
{
    /// <summary>
    /// Validates that no target framework / rid support is dropped in the latest package.
    /// Reports all the breaking changes in the latest package.
    /// </summary>
    public class BaselinePackageValidator
    {
        private readonly Package _baselinePackage;
        private readonly bool _runApiCompat;
        private readonly ApiCompatRunner _apiCompatRunner;
        private readonly CompatibilityLoggerBase _log;

        public BaselinePackageValidator(Package baselinePackage, bool runApiCompat, CompatibilityLoggerBase log, Dictionary<string, HashSet<string>> apiCompatReferences)
        {
            _baselinePackage = baselinePackage;
            _runApiCompat = runApiCompat;
            _log = log;
            _apiCompatRunner = new(enableStrictMode: false, _log, apiCompatReferences);
        }

        /// <summary>
        /// Validates the latest nuget package doesnot drop any target framework/rid and does not introduce any breaking changes.
        /// </summary>
        /// <param name="package">Nuget Package that needs to be validated.</param>
        public void Validate(Package package)
        {
            // Iterate over all available baseline assets
            foreach (ContentItem baselineCompileTimeAsset in _baselinePackage.RefAssets)
            {
                // Search for a compatible compile time asset in the latest package
                NuGetFramework baselineTargetFramework = (NuGetFramework)baselineCompileTimeAsset.Properties["tfm"];
                ContentItem? latestCompileTimeAsset = package.FindBestCompileAssetForFramework(baselineTargetFramework);
                if (latestCompileTimeAsset == null)
                {
                    _log.LogError(
                        new Suppression(DiagnosticIds.TargetFrameworkDropped) { Target = baselineTargetFramework.ToString() },
                        DiagnosticIds.TargetFrameworkDropped,
                        Resources.MissingTargetFramework,
                        baselineTargetFramework.ToString());
                }
                else if (_runApiCompat)
                {
                    string header = string.Format(Resources.ApiCompatibilityBaselineHeader, baselineCompileTimeAsset.Path, latestCompileTimeAsset.Path, _baselinePackage.Version, package.Version);
                    _apiCompatRunner.QueueApiCompatFromContentItem(package.PackageId, baselineCompileTimeAsset, latestCompileTimeAsset, header, isBaseline: true);
                }
            }

            // Iterates over both runtime and runtime specific baseline assets and searches for a compatible non runtime
            // specific asset in the latest package.
            foreach (ContentItem baselineRuntimeAsset in _baselinePackage.RuntimeAssets)
            {
                // Search for a compatible runtime asset in the latest package
                NuGetFramework baselineTargetFramework = (NuGetFramework)baselineRuntimeAsset.Properties["tfm"];
                ContentItem? latestRuntimeAsset = package.FindBestRuntimeAssetForFramework(baselineTargetFramework);
                if (latestRuntimeAsset == null)
                {
                    _log.LogError(
                        new Suppression(DiagnosticIds.TargetFrameworkDropped) { Target = baselineTargetFramework.ToString() },
                        DiagnosticIds.TargetFrameworkDropped,
                        Resources.MissingTargetFramework,
                        baselineTargetFramework.ToString());
                }
                else if (_runApiCompat)
                {
                    string header = string.Format(Resources.ApiCompatibilityBaselineHeader, baselineRuntimeAsset.Path, latestRuntimeAsset.Path, _baselinePackage.Version, package.Version);
                    _apiCompatRunner.QueueApiCompatFromContentItem(package.PackageId, baselineRuntimeAsset, latestRuntimeAsset, header, isBaseline: true);
                }
            }

            // Compares runtime specific baseline assets against runtime specific latest assets.
            foreach (ContentItem baselineRuntimeSpecificAsset in _baselinePackage.RuntimeSpecificAssets)
            {
                NuGetFramework baselineTargetFramework = (NuGetFramework)baselineRuntimeSpecificAsset.Properties["tfm"];
                string baselineRid = (string)baselineRuntimeSpecificAsset.Properties["rid"];
                ContentItem? latestRuntimeSpecificAsset = package.FindBestRuntimeAssetForFrameworkAndRuntime(baselineTargetFramework, baselineRid);
                if (latestRuntimeSpecificAsset == null)
                {
                    _log.LogError(
                        new Suppression(DiagnosticIds.TargetFrameworkAndRidPairDropped) { Target = baselineTargetFramework.ToString() + "-" + baselineRid },
                        DiagnosticIds.TargetFrameworkAndRidPairDropped,
                        Resources.MissingTargetFrameworkAndRid,
                        baselineTargetFramework.ToString(),
                        baselineRid);
                }
                else if (_runApiCompat)
                {
                    string header = string.Format(Resources.ApiCompatibilityBaselineHeader, baselineRuntimeSpecificAsset.Path, latestRuntimeSpecificAsset.Path, _baselinePackage.Version, package.Version);
                    _apiCompatRunner.QueueApiCompatFromContentItem(package.PackageId, baselineRuntimeSpecificAsset, latestRuntimeSpecificAsset, header, isBaseline: true);
                }
            }

            if (_runApiCompat)
                _apiCompatRunner.RunApiCompat(_baselinePackage.PackagePath, package.PackagePath);
        }
    }
}
