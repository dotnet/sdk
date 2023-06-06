// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.DotNet.PackageValidation
{
    /// <summary>
    /// A non mutable class that holds TargetFramework information and a collection of assembly references.
    /// </summary>
    public readonly struct PackageAssemblyReferenceCollection
    {
        /// <summary>
        /// The target framework moniker
        /// </summary>
        public string TargetFrameworkMoniker { get; }

        /// <summary>
        /// The target platform moniker. Can be empty.
        /// </summary>
        public string TargetPlatformMoniker { get; }

        /// <summary>
        /// The assembly references that belong to an assembly with the provided TargetFramework information.
        /// </summary>
        public IEnumerable<string> AssemblyReferences { get; }

        /// <summary>
        /// Creates a new instance of the <see cref="PackageAssemblyReferenceCollection"/>.
        /// </summary>
        public PackageAssemblyReferenceCollection(string targetFrameworkMoniker,
            string targetPlatformMoniker,
            IEnumerable<string> assemblyReferences)
        {
            TargetFrameworkMoniker = targetFrameworkMoniker;
            TargetPlatformMoniker = targetPlatformMoniker;
            AssemblyReferences = assemblyReferences;
        }
    }
}
