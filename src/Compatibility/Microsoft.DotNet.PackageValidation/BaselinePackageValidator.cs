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
    public class BaselinePackageValidator
    {
        private static string[] s_diagList = { DiagnosticIds.TargetFrameworkDropped, DiagnosticIds.TargetFrameworkAndRidPairDropped };

        private Package _baselinePackage;
        private bool _runApiCompat;
        private ApiCompatRunner _apiCompatRunner;
        private ILogger _log;
        private Checker _checker;

        public BaselinePackageValidator(Package baselinePackage, string noWarn, (string, string)[] ignoredDifferences, bool runApiCompat, ILogger log)
        {
            _baselinePackage = baselinePackage;
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
            foreach (ContentItem baselineCompileTimeAsset in _baselinePackage.CompileAssets)
            {
                NuGetFramework baselineTargetFramework = (NuGetFramework)baselineCompileTimeAsset.Properties["tfm"];
                ContentItem latestCompileTimeAsset = package.FindBestCompileAssetForFramework(baselineTargetFramework);
                if (latestCompileTimeAsset == null)
                {
                    if (!_checker.Contain(DiagnosticIds.TargetFrameworkDropped, baselineTargetFramework.ToString()))
                    {
                        string message = string.Format(Resources.MissingTargetFramework, baselineTargetFramework.ToString());
                        _log.LogError(DiagnosticIds.TargetFrameworkDropped + " " + message);
                    }
                }
                else
                {
                    if (_runApiCompat)
                    {
                        _apiCompatRunner.QueueApiCompat(_baselinePackage.PackagePath,
                            baselineCompileTimeAsset.Path,
                            package.PackagePath, 
                            latestCompileTimeAsset.Path,
                            Path.GetFileName(package.PackagePath),
                            Resources.BaselineVersionValidatorHeader,
                            string.Format(Resources.MissingApisForFramework, baselineTargetFramework.ToString()));
                    }
                }
            }

            foreach (ContentItem baselineRuntimeAsset in _baselinePackage.RuntimeAssets)
            {
                NuGetFramework baselineTargetFramework = (NuGetFramework)baselineRuntimeAsset.Properties["tfm"];
                ContentItem latestRuntimeAsset = package.FindBestRuntimeAssetForFramework(baselineTargetFramework);
                if (latestRuntimeAsset == null)
                {
                    if (!_checker.Contain(DiagnosticIds.TargetFrameworkDropped, baselineTargetFramework.ToString()))
                    {
                        string message = string.Format(Resources.MissingTargetFramework, baselineTargetFramework.ToString());
                        _log.LogError(DiagnosticIds.TargetFrameworkDropped + " " + message);
                    }
                }
                else
                {
                    if (_runApiCompat)
                    {
                        _apiCompatRunner.QueueApiCompat(_baselinePackage.PackagePath, 
                            baselineRuntimeAsset.Path,
                            package.PackagePath, 
                            latestRuntimeAsset.Path,
                            Path.GetFileName(package.PackagePath),
                            Resources.BaselineVersionValidatorHeader,
                            string.Format(Resources.MissingApisForFramework, baselineTargetFramework.ToString()));
                    }
                }
            }

            foreach (ContentItem baselineRuntimeSpecificAsset in _baselinePackage.RuntimeSpecificAssets)
            {
                NuGetFramework baselineTargetFramework = (NuGetFramework)baselineRuntimeSpecificAsset.Properties["tfm"];
                string baselineRid = (string)baselineRuntimeSpecificAsset.Properties["rid"];
                ContentItem latestRuntimeSpecificAsset = package.FindBestRuntimeAssetForFrameworkAndRuntime(baselineTargetFramework, baselineRid);
                if (latestRuntimeSpecificAsset == null)
                {
                    if (!_checker.Contain(DiagnosticIds.TargetFrameworkDropped, baselineTargetFramework.ToString() + "-" + baselineRid))
                    {
                        string message = string.Format(Resources.MissingTargetFrameworkAndRid, baselineTargetFramework.ToString(), baselineRid);
                        _log.LogError(DiagnosticIds.TargetFrameworkAndRidPairDropped + " " + message);
                    }
                }
                else
                {
                    if (_runApiCompat)
                    {
                        _apiCompatRunner.QueueApiCompat(_baselinePackage.PackagePath, 
                            baselineRuntimeSpecificAsset.Path,
                            package.PackagePath, 
                            latestRuntimeSpecificAsset.Path,
                            Path.GetFileName(package.PackagePath),
                            Resources.BaselineVersionValidatorHeader,
                            string.Format(Resources.MissingTargetFrameworkAndRid, baselineTargetFramework.ToString(), baselineRid));
                    }
                }
            }
            
            _apiCompatRunner.RunApiCompat();
        }
    }
}
