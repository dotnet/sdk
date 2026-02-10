// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Frameworks;

namespace Microsoft.DotNet.PackageValidation.Filtering
{
    /// <summary>
    /// Helper to check for excluded target frameworks with wildcard support.
    /// </summary>
    /// <param name="excludedTargetFrameworks">Target frameworks to exclude. The wildcard character '*' is allowed at the end of the string.</param>
    public class TargetFrameworkFilter(params string[] excludedTargetFrameworks) : ITargetFrameworkFilter
    {
        private const StringComparison TargetFrameworkComparison = StringComparison.InvariantCultureIgnoreCase;
        private readonly HashSet<string> _foundExcludedTargetFrameworks = [];

        /// <inheritdoc/>
        public IReadOnlyCollection<string> FoundExcludedTargetFrameworks => _foundExcludedTargetFrameworks;

        /// <inheritdoc/>
        public bool IsExcluded(NuGetFramework framework) => IsExcluded(framework.GetShortFolderName());

        /// <inheritdoc/>
        public bool IsExcluded(string targetFramework)
        {
            // Empty strings can't be excluded.
            if (targetFramework == string.Empty)
            {
                return false;
            }

            // Fast path if the target framework was already found.
            if (_foundExcludedTargetFrameworks.Contains(targetFramework))
            {
                return true;
            }

            foreach (string excludedTargetFramework in excludedTargetFrameworks)
            {
                // Wildcard match
                if (excludedTargetFramework.Length > 1 &&
#if NET
                    excludedTargetFramework.EndsWith('*'))
#else
                    excludedTargetFramework[excludedTargetFramework.Length - 1] == '*')
#endif
                {
                    string excludedTargetFrameworkWithoutWildcard = excludedTargetFramework.Substring(0, excludedTargetFramework.Length - 1);
                    if (targetFramework.StartsWith(excludedTargetFrameworkWithoutWildcard, TargetFrameworkComparison))
                    {
                        _foundExcludedTargetFrameworks.Add(targetFramework);
                        return true;
                    }
                }
                // Exact match
                else if (targetFramework.Equals(excludedTargetFramework, TargetFrameworkComparison))
                {
                    _foundExcludedTargetFrameworks.Add(targetFramework);
                    return true;
                }
            }

            return false;
        }
    }
}
