// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using NuGet.Common;
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
        private static string[] s_diagList =  { DiagnosticIds.CompatibleRuntimeRidLessAsset, DiagnosticIds.ApplicableCompileTimeAsset };
        private static Dictionary<NuGetFramework, HashSet<NuGetFramework>> s_packageTfmMapping = InitializeTfmMappings();

        private bool _runApiCompat;
        private ApiCompatRunner _apiCompatRunner;
        private Checker _checker;
        private ILogger _log;

        public CompatibleTfmValidator(string noWarn, (string, string)[] ignoredDifferences, bool runApiCompat, ILogger log)
        {
            _checker = new(noWarn, ignoredDifferences, s_diagList);
            _runApiCompat = runApiCompat;
            _log = log;
            _apiCompatRunner = new(noWarn, ignoredDifferences, _log);
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
                ContentItem compileTimeAsset = package.FindBestCompileAssetForFramework(framework);

                if (compileTimeAsset == null)
                {
                    if (!_checker.Contain(DiagnosticIds.ApplicableCompileTimeAsset, framework.ToString()))
                    {
                        string message = string.Format(Resources.NoCompatibleCompileTimeAsset, framework.ToString());
                        _log.LogError(DiagnosticIds.ApplicableCompileTimeAsset + " " + message);
                    }
                    break;
                }

                ContentItem runtimeAsset = package.FindBestRuntimeAssetForFramework(framework);
                if (runtimeAsset == null)
                {
                    if (!_checker.Contain(DiagnosticIds.CompatibleRuntimeRidLessAsset, framework.ToString()))
                    {
                        string message = string.Format(Resources.NoCompatibleRuntimeAsset, framework.ToString());
                        _log.LogError(DiagnosticIds.CompatibleRuntimeRidLessAsset + " " + message);
                    }
                }
                else
                {
                    if (_runApiCompat)
                    {
                        _apiCompatRunner.QueueApiCompat(package.PackagePath, 
                            compileTimeAsset.Path,
                            package.PackagePath,
                            runtimeAsset.Path,
                            Path.GetFileName(package.PackagePath),
                            Resources.CompatibleTfmValidatorHeader,
                            string.Format(Resources.MissingApisForFramework, framework.ToString()));
                    }
                }
 
                foreach (string rid in package.Rids)
                {
                    runtimeAsset = package.FindBestRuntimeAssetForFrameworkAndRuntime(framework, rid);
                    if (runtimeAsset == null)
                    {
                        if (!_checker.Contain(DiagnosticIds.CompatibleRuntimeRidSpecificAsset, framework.ToString() + "-" + rid))
                        {
                            string message = string.Format(Resources.NoCompatibleRidSpecificRuntimeAsset, framework.ToString(), rid);
                            _log.LogError(DiagnosticIds.CompatibleRuntimeRidSpecificAsset + " " + message);
                        }
                    }
                    else
                    {
                        if (_runApiCompat)
                        {
                            _apiCompatRunner.QueueApiCompat(package.PackagePath, 
                                compileTimeAsset.Path,
                                package.PackagePath, 
                                runtimeAsset.Path,
                                Path.GetFileName(package.PackagePath),
                                Resources.CompatibleTfmValidatorHeader,
                                string.Format(Resources.MissingApisForFrameworkAndRid, framework.ToString(), rid));
                        }
                    }
                }
            }

            _apiCompatRunner.RunApiCompat();
        }

        private static Dictionary<NuGetFramework, HashSet<NuGetFramework>> InitializeTfmMappings()
        {
            Dictionary<NuGetFramework, HashSet<NuGetFramework>> packageTfmMapping = new();
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
            return packageTfmMapping;
        }
    }
}
