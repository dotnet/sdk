// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    [TestClass]
    public class GivenAResolveReadyToRunCompilers
    {
        private const string RuntimeGraphContent = """
            {
                "runtimes": {
                    "any": {
                        "#import": []
                    },
                    "openharmony": {
                        "#import": ["any"]
                    },
                    "openharmony-arm64": {
                        "#import": ["openharmony"]
                    },
                    "openharmony-x64": {
                        "#import": ["openharmony"]
                    }
                }
            }
            """;

        [TestMethod]
        [DataRow("openharmony-arm64", "arm64")]
        [DataRow("openharmony-x64", "x64")]
        public void It_resolves_crossgen2_metadata_for_openharmony_rids(
            string runtimeIdentifier,
            string expectedArchitecture)
        {
            string packageDirectory = Path.Combine(Path.GetTempPath(), "r2r-" + Guid.NewGuid().ToString("N"));
            string runtimeGraphPath = Path.GetTempFileName();
            Directory.CreateDirectory(Path.Combine(packageDirectory, "tools"));

            try
            {
                File.WriteAllText(runtimeGraphPath, RuntimeGraphContent);
                File.WriteAllText(
                    Path.Combine(
                        packageDirectory,
                        "tools",
                        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "crossgen2.exe" : "crossgen2"),
                    string.Empty);

                TaskItem runtimePack = new(packageDirectory);
                runtimePack.SetMetadata(MetadataKeys.FrameworkName, "Microsoft.NETCore.App");
                runtimePack.SetMetadata(MetadataKeys.RuntimeIdentifier, runtimeIdentifier);
                runtimePack.SetMetadata(MetadataKeys.PackageDirectory, packageDirectory);

                TaskItem crossgen2Pack = new(packageDirectory);
                crossgen2Pack.SetMetadata(MetadataKeys.RuntimeIdentifier, "linux-x64");
                crossgen2Pack.SetMetadata(MetadataKeys.PackageDirectory, packageDirectory);
                crossgen2Pack.SetMetadata(MetadataKeys.NuGetPackageVersion, "11.0.0");

                ResolveReadyToRunCompilers task = new()
                {
                    BuildEngine = new MockBuildEngine(),
                    RuntimePacks = new ITaskItem[] { runtimePack },
                    Crossgen2Packs = new ITaskItem[] { crossgen2Pack },
                    TargetingPacks = Array.Empty<ITaskItem>(),
                    RuntimeGraphPath = runtimeGraphPath,
                    NETCoreSdkRuntimeIdentifier = "linux-x64",
                    ReadyToRunUseCrossgen2 = true
                };

                task.Execute().Should().BeTrue();
                task.Crossgen2Tool.Should().NotBeNull();
                task.Crossgen2Tool.GetMetadata(MetadataKeys.TargetOS).Should().Be("openharmony");
                task.Crossgen2Tool.GetMetadata(MetadataKeys.TargetArch).Should().Be(expectedArchitecture);
            }
            finally
            {
                File.Delete(runtimeGraphPath);
                Directory.Delete(packageDirectory, recursive: true);
            }
        }
    }
}
