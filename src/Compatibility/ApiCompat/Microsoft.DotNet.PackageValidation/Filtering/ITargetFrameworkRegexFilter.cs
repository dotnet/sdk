// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.PackageValidation.Filtering
{
    /// <summary>
    /// Helper to check for excluded target frameworks based on a regex pattern.
    /// </summary>
    public interface ITargetFrameworkRegexFilter
    {
        /// <summary>
        /// The list of found excluded target frameworks.
        /// </summary>
        IReadOnlyCollection<string> FoundExcludedTargetFrameworks { get; }

        /// <summary>
        /// Skip target frameworks that are excluded in the ExcludeTargetFrameworks filter.
        /// The comparison is performed invariant and with ignored casing.
        /// </summary>
        /// <param name="targetFramework">The target framework string to check for exclusion.</param>
        /// <returns>True if the target framework is excluded by the passed in pattern.</returns>
        bool IsExcluded(string targetFramework);
    }
}
