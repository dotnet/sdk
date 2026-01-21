// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Frameworks;

namespace Microsoft.DotNet.PackageValidation.Filtering
{
    /// <summary>
    /// Helper to check for excluded target frameworks with wildcard support.
    /// </summary>
    public interface ITargetFrameworkFilter
    {
        /// <summary>
        /// The list of found excluded target frameworks.
        /// </summary>
        IReadOnlyCollection<string> FoundExcludedTargetFrameworks { get; }

        /// <summary>
        /// Checks if a NuGetFramework is excluded based on the short folder name.
        /// The comparison is performed invariant and with ignored casing.
        /// </summary>
        /// <param name="framework">The NuGetFramework object to check for exclusion based on its short folder name.</param>
        /// <returns>True if the NuGetFramework is excluded.</returns>
        bool IsExcluded(NuGetFramework framework);

        /// <summary>
        /// Checks if a target framework string is excluded.
        /// The comparison is performed invariant and with ignored casing.
        /// </summary>
        /// <param name="targetFramework">The target framework string to check for exclusion.</param>
        /// <returns>True if the target framework is excluded.</returns>
        bool IsExcluded(string targetFramework);
    }
}
