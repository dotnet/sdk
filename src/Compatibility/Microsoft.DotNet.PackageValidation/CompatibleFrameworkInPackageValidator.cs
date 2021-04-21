// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Client;
using NuGet.ContentModel;
using NuGet.Frameworks;

namespace Microsoft.DotNet.PackageValidation
{
    /// <summary>
    /// Validates that the api surface of the compatible frameworks.
    /// </summary>
    public class CompatibleFrameworkInPackageValidator
    {
        private string _noWarn;
        private (string, string)[] _ignoredDifferences;
        ApiCompatRunner apiCompatRunner;

        public CompatibleFrameworkInPackageValidator(string noWarn, (string, string)[] ignoredDifferences)
        {
            _noWarn = noWarn;
            _ignoredDifferences = ignoredDifferences;
            apiCompatRunner = new(_noWarn, _ignoredDifferences);
        }

        /// <summary>
        /// Validates that the compatible frameworks have compatible surface area.
        /// </summary>
        /// <param name="package">Nuget Package that needs to be validated.</param>
        /// <returns>The List of Package Validation Diagnostics.</returns>
        public IEnumerable<ApiCompatDiagnostics> Validate(Package package)
        {       
            IEnumerable<ContentItem> compileAssets = package.CompileAssets.OrderByDescending(t => ((NuGetFramework)t.Properties["tfm"]).Version);
            ManagedCodeConventions conventions = new ManagedCodeConventions(null);
            Queue<ContentItem> compileAssetsQueue = new Queue<ContentItem>(compileAssets);


            while (compileAssetsQueue.Count > 0)
            {
                ContentItem compileTimeAsset = compileAssetsQueue.Dequeue();
                ContentItemCollection contentItemCollection = new();
                contentItemCollection.Load(compileAssetsQueue.Select(t => t.Path));

                NuGetFramework framework = (NuGetFramework)compileTimeAsset.Properties["tfm"];
                SelectionCriteria managedCriteria = conventions.Criteria.ForFramework(framework);

                ContentItem compatibleFrameworkAsset = null;
                if (package.HasRefAssemblies)
                {
                    compatibleFrameworkAsset = contentItemCollection.FindBestItemGroup(managedCriteria, conventions.Patterns.CompileRefAssemblies)?.Items.FirstOrDefault();
                }
                else
                {
                    compatibleFrameworkAsset = contentItemCollection.FindBestItemGroup(managedCriteria, conventions.Patterns.CompileLibAssemblies)?.Items.FirstOrDefault();

                }

                if (compatibleFrameworkAsset != null)
                {
                    apiCompatRunner.QueueApiCompat(Helpers.GetFileStreamFromPackage(package.PackagePath, compatibleFrameworkAsset.Path),
                        Helpers.GetFileStreamFromPackage(package.PackagePath, compileTimeAsset.Path),
                        Path.GetFileName(package.PackagePath),
                        Resources.CompatibleFrameworkInPackageValidatorHeader,
                        string.Format(Resources.MissingApisForFramework, framework.ToString()));
                }
            }
            return apiCompatRunner.RunApiCompat();
        }
    }
}
