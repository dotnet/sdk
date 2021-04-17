// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiCompatibility;
using NuGet.ContentModel;
using NuGet.Frameworks;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.DotNet.PackageValidation
{
    /// <summary>
    /// Validates that there are compile time and runtime assets for all the compatible frameworks.
    /// Queues the apicompat between the applicable compile and runtime assemblies for these frameworks.
    /// </summary>
    public class CompatibleTFMValidator : IPackageValidator
    {
        private string _noWarn;
        private (string, string)[] _ignoredDifferences;
        private bool _runApiCompat;
        private static Dictionary<NuGetFramework, HashSet<NuGetFramework>> packageTfmMapping = new();


        public CompatibleTFMValidator(string noWarn, (string, string)[] ignoredDifferences, bool runApiCompat)
        {
            _noWarn = noWarn;
            _ignoredDifferences = ignoredDifferences;
            _runApiCompat = runApiCompat;
        }

        static CompatibleTFMValidator()
        {
            Initialize();
        }

        public DiagnosticBag<TargetFrameworkApplicabilityDiagnostics> Validate(Package package)
        {
            DiagnosticBag<TargetFrameworkApplicabilityDiagnostics> errors = new DiagnosticBag<TargetFrameworkApplicabilityDiagnostics>(_noWarn, _ignoredDifferences);

            HashSet<NuGetFramework> compatibleTargetFrameworks = new();
            foreach (NuGetFramework item in package.FrameworksInPackage)
            {
                if (packageTfmMapping.ContainsKey(item))
                    compatibleTargetFrameworks.UnionWith(packageTfmMapping[item]);
            }

            foreach (NuGetFramework framework in compatibleTargetFrameworks)
            {
                ContentItem compileTimeAsset = package.FindBestCompileAssetForFramework(framework);
                if (compileTimeAsset == null)
                {
                    errors.Add(new TargetFrameworkApplicabilityDiagnostics(DiagnosticIds.ApplicableCompileTimeAsset,
                        framework.ToString(),
                        string.Format(Resources.NoCompatibleCompileTimeAsset, framework.ToString())));
                }

                ContentItem runtimeAsset = package.FindBestRuntimeAssetForFramework(framework);
                if (runtimeAsset == null)
                {
                    errors.Add(new TargetFrameworkApplicabilityDiagnostics(DiagnosticIds.ApplicableRidLessAsset,
                        framework.ToString(),
                        string.Format(Resources.NoCompatibleRuntimeAsset, framework.ToString())));
                }

                if (runtimeAsset != null && compileTimeAsset != null)
                {
                    if (_runApiCompat)
                    {
                        ApiCompatRunner.QueueApiCompat(Helpers.GetFileStreamFromPackage(package.PackagePath, compileTimeAsset.Path),
                            Helpers.GetFileStreamFromPackage(package.PackagePath, runtimeAsset.Path),
                            Path.GetFileName(package.PackagePath),
                            nameof(CompatibleTFMValidator),
                            string.Format(Resources.MissingApisForFramework, framework.ToString()));
                    }
                }

                foreach (string rid in package.Rids)
                {
                    runtimeAsset = package.FindBestRuntimeAssetForFrameworkAndRuntime(framework, rid);
                    if (runtimeAsset == null)
                    {
                        errors.Add(new TargetFrameworkApplicabilityDiagnostics(DiagnosticIds.ApplicableRuntimeSpecificAsset,
                            $"{framework}-" + rid,
                            string.Format(Resources.NoCompatibleRidSpecificRuntimeAsset, framework.ToString(), rid)));
                    }
                    else
                    {
                        if (_runApiCompat)
                        {
                            ApiCompatRunner.QueueApiCompat(Helpers.GetFileStreamFromPackage(package.PackagePath, compileTimeAsset.Path),
                                Helpers.GetFileStreamFromPackage(package.PackagePath, runtimeAsset.Path),
                                Path.GetFileName(package.PackagePath),
                                nameof(CompatibleTFMValidator),
                                string.Format(Resources.MissingApisForFrameworkAndRid, framework.ToString(), rid));
                        }
                    }
                }
            }
            return errors;
        }

        public static void Initialize()
        {
            // creating a map framework in package => frameworks to test based on default compatibilty mapping.
            foreach (var item in DefaultFrameworkMappings.Instance.CompatibilityMappings)
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
        }
    }
}
