// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace Microsoft.DotNet.Cli.Tests;

/// <summary>
/// MSBuild.dll ships in the SDK redist layout (alongside dotnet.dll) but is not listed
/// in dotnet.Tests.deps.json. When test code reaches into types that live in MSBuild.dll
/// (e.g. <c>Microsoft.Build.CommandLine.Experimental.CommandLineParser</c>) the default
/// <see cref="AssemblyLoadContext"/> cannot resolve it and throws
/// <see cref="FileNotFoundException"/>. Because the test process runs from
/// <c>$(TestHostFolder)</c> (the redist layout root), MSBuild.dll is already next to the
/// test assembly on disk; this initializer just tells the loader where to find it.
/// </summary>
internal static class MSBuildAssemblyResolver
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        AssemblyLoadContext.Default.Resolving += static (context, assemblyName) =>
        {
            if (!string.Equals(assemblyName.Name, "MSBuild", StringComparison.Ordinal))
            {
                return null;
            }

            string candidate = Path.Combine(AppContext.BaseDirectory, "MSBuild.dll");
            return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
        };
    }
}
