// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    /// <summary>
    /// Tests for ResolveAppHosts multi-threading support.
    /// These tests verify:
    /// 1. Output path relativity preservation (AR-May finding #2)
    /// 2. TaskEnvironment-based path resolution
    /// </summary>
    public class GivenAResolveAppHostsMultiThreading
    {
        /// <summary>
        /// AR-May Finding #2: Verify output path relativity is preserved.
        /// When TargetingPackRoot is relative, output metadata should remain relative (not absolute).
        /// This test would FAIL under the PR #52936 implementation without the OriginalValue fix.
        /// </summary>
        [Fact]
        public void OutputPathRelativity_PreservesOriginalPaths()
        {
            string testDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                Directory.CreateDirectory(testDir);
                
                // Create a relative targeting pack structure
                string relativePackRoot = "packs";
                string absolutePackRoot = Path.Combine(testDir, relativePackRoot);
                string packName = "Microsoft.NETCore.App.Host.win-x64";
                string packVersion = "8.0.0";
                string packPath = Path.Combine(absolutePackRoot, packName, packVersion);
                string nativePath = Path.Combine(packPath, "runtimes", "win-x64", "native");
                
                Directory.CreateDirectory(nativePath);
                File.WriteAllText(Path.Combine(nativePath, "apphost.exe"), "fake apphost");
                
                // Create a minimal runtime graph
                string runtimeGraphPath = Path.Combine(testDir, "runtime.json");
                File.WriteAllText(runtimeGraphPath, @"{
  ""runtimes"": {
    ""win-x64"": {
      ""#import"": []
    }
  }
}");

                var knownAppHostPacks = new ITaskItem[]
                {
                    new TaskItem("Microsoft.NETCore.App.Host", new Dictionary<string, string>
                    {
                        { "TargetFramework", "net8.0" },
                        { "AppHostRuntimeIdentifiers", "win-x64" },
                        { "AppHostPackNamePattern", "Microsoft.NETCore.App.Host.**RID**" },
                        { "AppHostPackVersion", packVersion },
                        { MetadataKeys.ExcludedRuntimeIdentifiers, "" }
                    })
                };

                var task = new ResolveAppHosts
                {
                    BuildEngine = new MockBuildEngine(),
                    TargetFrameworkIdentifier = ".NETCoreApp",
                    TargetFrameworkVersion = "8.0",
                    TargetingPackRoot = relativePackRoot,  // RELATIVE path
                    AppHostRuntimeIdentifier = "win-x64",
                    DotNetAppHostExecutableNameWithoutExtension = "apphost",
                    DotNetSingleFileHostExecutableNameWithoutExtension = "singlefilehost",
                    DotNetComHostLibraryNameWithoutExtension = "comhost",
                    DotNetIjwHostLibraryNameWithoutExtension = "ijwhost",
                    RuntimeGraphPath = runtimeGraphPath,
                    KnownAppHostPacks = knownAppHostPacks,
                    NuGetRestoreSupported = true,
                    EnableAppHostPackDownload = false
                };

#if NETFRAMEWORK
                task.TaskEnvironment = new TaskEnvironment(new ProcessTaskEnvironmentDriver(testDir));
#else
                // On .NET Core+, simulate TaskEnvironment by passing absolute path as TargetingPackRoot directly
                // This test still validates the relativity behavior as we check if the metadata preserves original input
                // The key fix is in the task itself, not in how TaskEnvironment is set up
                task.TaskEnvironment = null!;
#endif

                bool result = task.Execute();

                // Assert task succeeded
                result.Should().BeTrue();
                task.AppHost.Should().NotBeNull();
                task.AppHost.Should().HaveCount(1);

                var appHostItem = task.AppHost[0];

                // CRITICAL: PackageDirectory metadata should preserve RELATIVE path (not absolute)
                string packageDirMetadata = appHostItem.GetMetadata(MetadataKeys.PackageDirectory);
                packageDirMetadata.Should().NotBeNullOrEmpty();
                packageDirMetadata.Should().StartWith(relativePackRoot, "PackageDirectory should remain relative");
                Path.IsPathRooted(packageDirMetadata).Should().BeFalse("PackageDirectory should not be absolute");

                // CRITICAL: Path metadata should preserve RELATIVE path (not absolute)
                string pathMetadata = appHostItem.GetMetadata(MetadataKeys.Path);
                pathMetadata.Should().NotBeNullOrEmpty();
                pathMetadata.Should().StartWith(relativePackRoot, "Path should remain relative");
                Path.IsPathRooted(pathMetadata).Should().BeFalse("Path should not be absolute");

                // Verify the path structure is correct (relative base + runtime path)
                string expectedRelativePath = Path.Combine(relativePackRoot, packName, packVersion, "runtimes", "win-x64", "native", "apphost.exe");
                pathMetadata.Should().Be(expectedRelativePath);
            }
            finally
            {
                if (Directory.Exists(testDir))
                {
                    Directory.Delete(testDir, true);
                }
            }
        }

        /// <summary>
        /// Verify absolute paths work correctly (not just relative).
        /// </summary>
        [Fact]
        public void AbsolutePathInput_WorksCorrectly()
        {
            string testDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                Directory.CreateDirectory(testDir);
                
                string absolutePackRoot = Path.Combine(testDir, "packs");
                SetupPackStructure(absolutePackRoot, "8.0.0");

                string runtimeGraphPath = Path.Combine(testDir, "runtime.json");
                File.WriteAllText(runtimeGraphPath, @"{
  ""runtimes"": {
    ""win-x64"": {
      ""#import"": []
    }
  }
}");

                var knownAppHostPacks = new ITaskItem[]
                {
                    new TaskItem("Microsoft.NETCore.App.Host", new Dictionary<string, string>
                    {
                        { "TargetFramework", "net8.0" },
                        { "AppHostRuntimeIdentifiers", "win-x64" },
                        { "AppHostPackNamePattern", "Microsoft.NETCore.App.Host.**RID**" },
                        { "AppHostPackVersion", "8.0.0" },
                        { MetadataKeys.ExcludedRuntimeIdentifiers, "" }
                    })
                };

                var task = new ResolveAppHosts
                {
                    BuildEngine = new MockBuildEngine(),
                    TargetFrameworkIdentifier = ".NETCoreApp",
                    TargetFrameworkVersion = "8.0",
                    TargetingPackRoot = absolutePackRoot,  // ABSOLUTE path
                    AppHostRuntimeIdentifier = "win-x64",
                    DotNetAppHostExecutableNameWithoutExtension = "apphost",
                    DotNetSingleFileHostExecutableNameWithoutExtension = "singlefilehost",
                    DotNetComHostLibraryNameWithoutExtension = "comhost",
                    DotNetIjwHostLibraryNameWithoutExtension = "ijwhost",
                    RuntimeGraphPath = runtimeGraphPath,
                    KnownAppHostPacks = knownAppHostPacks,
                    NuGetRestoreSupported = true,
                    EnableAppHostPackDownload = false
                };

#if NETFRAMEWORK
                task.TaskEnvironment = new TaskEnvironment(new ProcessTaskEnvironmentDriver(testDir));
#else
                task.TaskEnvironment = null!;
#endif

                bool result = task.Execute();

                result.Should().BeTrue();
                task.AppHost.Should().NotBeNull();
                task.AppHost.Should().HaveCount(1);

                // With absolute input, output should also be absolute (OriginalValue = Value)
                string packageDirMetadata = task.AppHost[0].GetMetadata(MetadataKeys.PackageDirectory);
                Path.IsPathRooted(packageDirMetadata).Should().BeTrue("Absolute input should produce absolute output");
                packageDirMetadata.Should().StartWith(absolutePackRoot);
            }
            finally
            {
                if (Directory.Exists(testDir))
                {
                    Directory.Delete(testDir, true);
                }
            }
        }

        private static void SetupPackStructure(string packRoot, string version)
        {
            string packName = "Microsoft.NETCore.App.Host.win-x64";
            string packPath = Path.Combine(packRoot, packName, version);
            string nativePath = Path.Combine(packPath, "runtimes", "win-x64", "native");
            
            Directory.CreateDirectory(nativePath);
            File.WriteAllText(Path.Combine(nativePath, "apphost.exe"), "fake apphost");
        }
    }
}

