// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiCompatibility;
using NuGet.ContentModel;
using NuGet.Frameworks;
using System.IO;

namespace Microsoft.DotNet.PackageValidation
{
    /// <summary>
    /// Validates that there is a runtime asset for every compile time asset in the package.
    /// Queues the apicompat between runtime and compile time assemblies.
    /// </summary>
    public class CompileTimeValidator : IPackageValidator
    {
        private string _noWarn;
        private (string, string)[] _ignoredDifferences;
        private bool _runApiCompat;

        public CompileTimeValidator(string noWarn, (string, string)[] ignoredDifferences, bool runApiCompat)
        {
            _noWarn = noWarn;
            _ignoredDifferences = ignoredDifferences;
            _runApiCompat = runApiCompat;
        }

        public DiagnosticBag<TargetFrameworkApplicabilityDiagnostics> Validate(Package package)
        {
            DiagnosticBag<TargetFrameworkApplicabilityDiagnostics> errors = new DiagnosticBag<TargetFrameworkApplicabilityDiagnostics>(_noWarn, _ignoredDifferences);

            foreach (ContentItem compileTimeAsset in package.CompileAssets)
            {
                NuGetFramework compileTimeFramework = (NuGetFramework)compileTimeAsset.Properties["tfm"];
                ContentItem runtimeAsset = package.FindBestRuntimeAssetForFramework(compileTimeFramework);

                if (runtimeAsset == null)
                {
                    errors.Add(new TargetFrameworkApplicabilityDiagnostics(DiagnosticIds.CompatibleRuntimeRidLessAsset,
                        compileTimeFramework.ToString(),
                        string.Format(Resources.NoCompatibleRuntimeAsset, compileTimeFramework.ToString())));
                }
                else
                {
                    if (_runApiCompat)
                    {
                        ApiCompatRunner.QueueApiCompat(Helpers.GetFileStreamFromPackage(package.PackagePath, compileTimeAsset.Path),
                            Helpers.GetFileStreamFromPackage(package.PackagePath, runtimeAsset.Path),
                            Path.GetFileName(package.PackagePath),
                            nameof(CompileTimeValidator),
                            string.Format(Resources.MissingApisForFramework, compileTimeFramework.ToString()));
                    }
                }

                foreach (var rid in package.Rids)
                {
                    ContentItem runtimeSpecificAsset = package.FindBestRuntimeAssetForFrameworkAndRuntime(compileTimeFramework, rid);
                    if (runtimeSpecificAsset == null)
                    {
                        errors.Add(new TargetFrameworkApplicabilityDiagnostics(DiagnosticIds.CompatibleRuntimeRidSpecificAsset,
                            $"{compileTimeFramework}-" + rid,
                            string.Format(Resources.NoCompatibleRidSpecificRuntimeAsset, compileTimeFramework.ToString(), rid)));
                    }
                    else
                    {
                        if (_runApiCompat)
                        {
                            ApiCompatRunner.QueueApiCompat(Helpers.GetFileStreamFromPackage(package.PackagePath, compileTimeAsset.Path),
                                Helpers.GetFileStreamFromPackage(package.PackagePath, runtimeSpecificAsset.Path),
                                Path.GetFileName(package.PackagePath),
                                nameof(CompileTimeValidator),
                                string.Format(Resources.MissingApisForFrameworkAndRid, compileTimeFramework.ToString(), rid));
                        }
                    }
                }
            }
            return errors;
        }
    }
}
