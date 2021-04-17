// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiCompatibility;
using NuGet.ContentModel;
using NuGet.Frameworks;
using System.IO;

namespace Microsoft.DotNet.PackageValidation
{
    /// <summary>
    /// Validates that no target framework / rid support is dropped in the latest package.
    /// Reports all the breaking changes in the latest package.
    /// </summary>
    public class PreviousVersionValidator : IPackageValidator
    {
        private Package _previousPackage;
        private string _noWarn;
        private (string, string)[] _ignoredDifferences;
        private bool _runApiCompat;

        public PreviousVersionValidator(Package package, string noWarn, (string, string)[] ignoredDifferences, bool runApiCompat)
        {
            MemoryStream packageStream =  package.DownloadLatestStableVersionAsync().Result;
            _previousPackage = NupkgParser.CreatePackage(packageStream, null);
            _noWarn = noWarn;
            _ignoredDifferences = ignoredDifferences;
            _runApiCompat = runApiCompat;
        }

        public DiagnosticBag<TargetFrameworkApplicabilityDiagnostics> Validate(Package package)
        {
            DiagnosticBag<TargetFrameworkApplicabilityDiagnostics> errors = new DiagnosticBag<TargetFrameworkApplicabilityDiagnostics>(_noWarn, _ignoredDifferences);

            foreach (ContentItem previousCompileTimeAsset in _previousPackage.CompileAssets)
            {
                NuGetFramework previousTargetFramework = (NuGetFramework)previousCompileTimeAsset.Properties["tfm"];
                ContentItem latestCompileTimeAsset = package.FindBestCompileAssetForFramework(previousTargetFramework);
                if (latestCompileTimeAsset == null)
                {
                    errors.Add(new TargetFrameworkApplicabilityDiagnostics(DiagnosticIds.TargetFrameworkDropped,
                        previousTargetFramework.ToString(),
                        string.Format(Resources.MissingTargetFramework, previousTargetFramework.ToString())));
                }
                else
                {
                    if (_runApiCompat)
                    {
                        ApiCompatRunner.QueueApiCompat(Helpers.GetFileStreamFromPackage(_previousPackage.PackageStream, previousCompileTimeAsset.Path),
                            Helpers.GetFileStreamFromPackage(package.PackagePath, latestCompileTimeAsset.Path),
                            Path.GetFileName(package.PackagePath),
                            nameof(PreviousVersionValidator),
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
                    errors.Add(new TargetFrameworkApplicabilityDiagnostics(DiagnosticIds.TargetFrameworkDropped,
                        previousTargetFramework.ToString(),
                        string.Format(Resources.MissingTargetFramework, previousTargetFramework.ToString())));
                }
                else
                {
                    if (_runApiCompat)
                    {
                        ApiCompatRunner.QueueApiCompat(Helpers.GetFileStreamFromPackage(_previousPackage.PackageStream, previousRuntimeAsset.Path),
                            Helpers.GetFileStreamFromPackage(package.PackagePath, latestRuntimeAsset.Path),
                            Path.GetFileName(package.PackagePath),
                            nameof(PreviousVersionValidator),
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
                    errors.Add(new TargetFrameworkApplicabilityDiagnostics(DiagnosticIds.TargetFrameworkAndRidPairDropped,
                        $"{previousTargetFramework}-" + previousRid, 
                        string.Format(Resources.MissingTargetFrameworkAndRid , previousTargetFramework.ToString(), previousRid)));
                }
                else
                {
                    if (_runApiCompat)
                    {
                        ApiCompatRunner.QueueApiCompat(Helpers.GetFileStreamFromPackage(_previousPackage.PackageStream, previousRuntimeSpecificAsset.Path),
                            Helpers.GetFileStreamFromPackage(package.PackagePath, latestRuntimeSpecificAsset.Path),
                            Path.GetFileName(package.PackagePath),
                            nameof(PreviousVersionValidator),
                            string.Format(Resources.MissingTargetFrameworkAndRid, previousTargetFramework.ToString(), previousRid));
                    }
                }
            }
            return errors;
        }
    }
}
