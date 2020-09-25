// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Reflection;
using System.Runtime.Loader;

#nullable enable

namespace Microsoft.CodeAnalysis.Tools.Tests.Utilities
{
    internal sealed class NuGetLoader
    {
        private static readonly string[] s_nugetAssemblyName = new[]
        {
            "NuGet.Common",
            "NuGet.Configuration",
            "NuGet.Frameworks",
            "NuGet.Packaging",
            "NuGet.Protocol",
            "NuGet.Versioning",
        };

        public static void Load()
        {
            foreach (var assemblyName in s_nugetAssemblyName)
            {
                AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(assemblyName));
            }
        }
    }
}
