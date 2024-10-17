// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Frameworks;

namespace Microsoft.DotNet.PackageValidation
{
    internal static class PackageExtensions
    {
        public static bool SupportsRuntimeIdentifier(this NuGetFramework tfm, string rid) =>
            tfm.Framework != ".NETFramework" || rid.StartsWith("win", StringComparison.OrdinalIgnoreCase);
    }
}
