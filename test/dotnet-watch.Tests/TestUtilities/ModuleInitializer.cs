// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Microsoft.Build.Locator;

namespace Microsoft.DotNet.Watch.UnitTests;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        // Ensure that we load the msbuild binaries from redist deployment. Otherwise, msbuild might use target files
        // that do not match the implementations of the core tasks.

        // If this throws make sure the test assembly, or any of its dependencies copied to this project's output directory,
        // does not have any public type that has a dependency on msbuild.
        // xUnit loads all public types and any reference to msbuild assembly will trigger its load.

        var toolset = TestContext.Current.ToolsetUnderTest;
        var sdkDir = toolset.SdkFolderUnderTest;
        var watchDir = Path.Combine(sdkDir, "DotnetTools", "dotnet-watch", toolset.SdkVersion, "tools", ToolsetInfo.CurrentTargetFramework, "any");

        MSBuildLocator.RegisterMSBuildPath(sdkDir);

        // The project references of this project are set up so that some dependencies are not copied to the output directory.
        // These need to be resolved in one of the Redist directories.

        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            var path = GetPath(name, sdkDir);
            if (!File.Exists(path))
            {
                path = GetPath(name, watchDir);
                if (!File.Exists(path))
                {
                    return null;
                }
            }

            return AssemblyLoadContext.Default.LoadFromAssemblyPath(path);

            static string GetPath(AssemblyName name, string dir)
            {
                var path = dir;
                if (name.CultureName != null)
                {
                    path = Path.Combine(path, name.CultureName);
                }

                path = Path.Combine(path, name.Name + ".dll");
                return path;
            }
        };
    }
}
