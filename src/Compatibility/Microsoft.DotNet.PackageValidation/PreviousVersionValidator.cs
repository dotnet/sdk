// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using NuGet.Common;
using NuGet.ContentModel;
using NuGet.Frameworks;

namespace Microsoft.DotNet.PackageValidation
{
    /// <summary>
    /// Validates that no target framework / rid support is dropped in the latest package.
    /// Reports all the breaking changes in the latest package.
    /// </summary>
    public class PreviousVersionValidator
    {
        private static string[] s_diagList = { DiagnosticIds.TargetFrameworkDropped, DiagnosticIds.TargetFrameworkAndRidPairDropped };

        private Package _previousPackage;
        private bool _runApiCompat;
        private ApiCompatRunner _apiCompatRunner;
        private ILogger _log;
        private Checker _checker;

        public PreviousVersionValidator(Package previousPackage, string noWarn, (string, string)[] ignoredDifferences, bool runApiCompat, ILogger log)
        {
            _previousPackage = previousPackage;
            _runApiCompat = runApiCompat;
            _log = log;
            _apiCompatRunner = new(noWarn, ignoredDifferences, _log);
            _checker = new(noWarn, ignoredDifferences, s_diagList);
        }

        /// <summary>
        /// Validates the latest nuget package doesnot drop any target framework/rid and does not introduce any breaking changes.
        /// </summary>
        /// <param name="package">Nuget Package that needs to be validated.</param>
        public void Validate(Package package)
        {
            foreach (ContentItem previousCompileTimeAsset in _previousPackage.CompileAssets)
            {
                NuGetFramework previousTargetFramework = (NuGetFramework)previousCompileTimeAsset.Properties["tfm"];
                ContentItem latestCompileTimeAsset = package.FindBestCompileAssetForFramework(previousTargetFramework);
                if (latestCompileTimeAsset == null)
                {
                    if (!_checker.Contain(DiagnosticIds.TargetFrameworkDropped, previousTargetFramework.ToString()))
                    {
                        string message = string.Format(Resources.MissingTargetFramework, previousTargetFramework.ToString());
                        _log.LogError(DiagnosticIds.TargetFrameworkDropped + " " + message);
                    }
                }
                else
                {
                    if (_runApiCompat)
                    {
                        _apiCompatRunner.QueueApiCompat(_previousPackage.PackagePath,
                            previousCompileTimeAsset.Path,
                            package.PackagePath, 
                            latestCompileTimeAsset.Path,
                            Path.GetFileName(package.PackagePath),
                            Resources.PreviousVersionValidatorHeader,
                            string.Format(Resources.MissingApisForFramework, previousTargetFramework.ToString()));
                    }
                }
            }

            foreach (ContentItem previousRuntimeAsset in _previousPackage.RuntimeAssets)
            {
                NuGetFramework previousTargetFramework = (NuGetFramework)previousRuntimeAsset.Properties["tfm"];
                ContentItem latestRuntimeAsset = package.FindBestRuntimeAssetForFramework(previousTargetFramework);
                if (latestRuntimeAsset == null)
                {
                    if (!_checker.Contain(DiagnosticIds.TargetFrameworkDropped, previousTargetFramework.ToString()))
                    {
                        string message = string.Format(Resources.MissingTargetFramework, previousTargetFramework.ToString());
                        _log.LogError(DiagnosticIds.TargetFrameworkDropped + " " + message);
                    }
                }
                else
                {
                    if (_runApiCompat)
                    {
                        _apiCompatRunner.QueueApiCompat(_previousPackage.PackagePath, 
                            previousRuntimeAsset.Path,
                            package.PackagePath, 
                            latestRuntimeAsset.Path,
                            Path.GetFileName(package.PackagePath),
                            Resources.PreviousVersionValidatorHeader,
                            string.Format(Resources.MissingApisForFramework, previousTargetFramework.ToString()));
                    }
                }
            }

            foreach (ContentItem previousRuntimeSpecificAsset in _previousPackage.RuntimeSpecificAssets)
            {
                NuGetFramework previousTargetFramework = (NuGetFramework)previousRuntimeSpecificAsset.Properties["tfm"];
                string previousRid = (string)previousRuntimeSpecificAsset.Properties["rid"];
                ContentItem latestRuntimeSpecificAsset = package.FindBestRuntimeAssetForFrameworkAndRuntime(previousTargetFramework, previousRid);
                if (latestRuntimeSpecificAsset == null)
                {
                    if (!_checker.Contain(DiagnosticIds.TargetFrameworkDropped, previousTargetFramework.ToString() + "-" + previousRid))
                    {
                        string message = string.Format(Resources.MissingTargetFrameworkAndRid, previousTargetFramework.ToString(), previousRid);
                        _log.LogError(DiagnosticIds.TargetFrameworkAndRidPairDropped + " " + message);
                    }
                }
                else
                {
                    if (_runApiCompat)
                    {
                        _apiCompatRunner.QueueApiCompat(_previousPackage.PackagePath, 
                            previousRuntimeSpecificAsset.Path,
                            package.PackagePath, 
                            latestRuntimeSpecificAsset.Path,
                            Path.GetFileName(package.PackagePath),
                            Resources.PreviousVersionValidatorHeader,
                            string.Format(Resources.MissingTargetFrameworkAndRid, previousTargetFramework.ToString(), previousRid));
                    }
                }
            }
            
            _apiCompatRunner.RunApiCompat();
        }
    }
}
