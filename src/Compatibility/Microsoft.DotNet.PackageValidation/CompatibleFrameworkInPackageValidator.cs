// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ApiCompatibility;
using NuGet.Client;
using NuGet.ContentModel;
using NuGet.Frameworks;

namespace Microsoft.DotNet.PackageValidation
{
    /// <summary>
    /// Validates that the api surface of the compatible frameworks.
    /// </summary>
    public class CompatibleFrameworkInPackageValidator : IPackageValidator
    {
        private string _noWarn;
        private (string, string)[] _ignoredDifferences;
        
        public CompatibleFrameworkInPackageValidator(string noWarn, (string, string)[] ignoredDifferences)
        {
            _noWarn = noWarn;
            _ignoredDifferences = ignoredDifferences;
        }

        public DiagnosticBag<TargetFrameworkApplicabilityDiagnostics> Validate(Package package)
        {       
            IEnumerable<ContentItem> compileAssets = package.CompileAssets.OrderByDescending(t => ((NuGetFramework)t.Properties["tfm"]).Version);
            ManagedCodeConventions conventions = new ManagedCodeConventions(null);

            while (compileAssets.Count() != 1)
            {
                ContentItem compileTimeAsset = GetNextItemToRemove(compileAssets);
                compileAssets = compileAssets.Where(t => t.Path != compileTimeAsset.Path);

                ContentItemCollection contentItemCollection = new();
                contentItemCollection.Load(compileAssets.Select(t => t.Path));

                NuGetFramework framework = (NuGetFramework)compileTimeAsset.Properties["tfm"];
                SelectionCriteria managedCriteria = conventions.Criteria.ForFramework(framework);
                ContentItem compatibleFrameworkAsset = contentItemCollection.FindBestItemGroup(managedCriteria, conventions.Patterns.CompileRefAssemblies)?.Items.FirstOrDefault();

                if (compatibleFrameworkAsset != null)
                {
                    ApiCompatRunner.QueueApiCompat(Helpers.GetFileStreamFromPackage(package.PackagePath, compileTimeAsset.Path),
                        Helpers.GetFileStreamFromPackage(package.PackagePath, compatibleFrameworkAsset.Path),
                        Path.GetFileName(package.PackagePath),
                        nameof(CompatibleFrameworkInPackageValidator),
                        string.Format(Resources.MissingApisForFramework, framework.ToString()));
                }
            }
            return null;
        }

        private static ContentItem GetNextItemToRemove(IEnumerable<ContentItem> assets)
        {
            IEnumerable<ContentItem> NonNetStandardAssets = assets.Where(t => ((NuGetFramework)t.Properties["tfm"]).Framework != ".NETStandard");
            if (NonNetStandardAssets == null)
            {
                return assets.First();

            }
            else
            {
                return NonNetStandardAssets.First();
            }
        }
    }
}
