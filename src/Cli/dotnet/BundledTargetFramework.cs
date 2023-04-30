// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Runtime.Versioning;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli
{
    internal static class BundledTargetFramework
    {
        public static string GetTargetFrameworkMoniker()
        {
            TargetFrameworkAttribute targetFrameworkAttribute = typeof(BundledTargetFramework)
                .GetTypeInfo()
                .Assembly
                .GetCustomAttribute<TargetFrameworkAttribute>();

            return NuGetFramework
                .Parse(targetFrameworkAttribute.FrameworkName)
                .GetShortFolderName();
        }
    }
}
