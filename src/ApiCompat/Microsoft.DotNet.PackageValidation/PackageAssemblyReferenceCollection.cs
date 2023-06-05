// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.DotNet.PackageValidation
{
    public readonly struct PackageAssemblyReferenceCollection
    {
        public string TargetFrameworkMoniker { get; }

        public string? TargetPlatformMoniker { get; }

        public IEnumerable<string> AssemblyReferences { get; }

        public PackageAssemblyReferenceCollection(string targetFrameworkMoniker,
            string? targetPlatformMoniker,
            IEnumerable<string> assemblyReferences)
        {
            TargetFrameworkMoniker = targetFrameworkMoniker;
            TargetPlatformMoniker = targetPlatformMoniker;
            AssemblyReferences = assemblyReferences;
        }
    }
}
