// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using FluentAssertions;
using Microsoft.Build.Utilities;
using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAResolveRuntimePackAssetsTask : SdkTest
    {
        public GivenAResolveRuntimePackAssetsTask(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void ItFiltersSatelliteResources()
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory().Path;

            var task = new ResolveRuntimePackAssets()
            {
                BuildEngine = new MockBuildEngine(),
                FrameworkReferences = new TaskItem[] { new TaskItem("TestFramework") },
                ResolvedRuntimePacks = new TaskItem[]
                {
                    new TaskItem("TestRuntimePack",
                    new Dictionary<string, string> {
                        { "FrameworkName", "TestFramework" },
                        { "RuntimeIdentifier", "test-rid" },
                        { "PackageDirectory", testDirectory },
                        { "PackageVersion", "0.1.0" },
                        { "IsTrimmable", "false" }
                    })
                },
                SatelliteResourceLanguages = new TaskItem[] { new TaskItem("de") }
            };

            Directory.CreateDirectory(Path.Combine(testDirectory, "data"));

            File.WriteAllText(
                Path.Combine(testDirectory, "data", "RuntimeList.xml"),
@"<FileList Name="".NET Core 3.1"" TargetFrameworkIdentifier="".NETCoreApp"" TargetFrameworkVersion=""3.1"" FrameworkName=""Microsoft.NETCore.App"">
  <File Type=""Resources"" Path=""runtimes/de/a.resources.dll"" Culture=""de"" FileVersion=""0.0.0.0"" />
  <File Type=""Resources"" Path=""runtimes/cs/a.resources.dll"" Culture=""cs"" FileVersion=""0.0.0.0"" />
</FileList>");

            task.Execute();
            task.RuntimePackAssets.Should().HaveCount(1);
            string expectedResource = Path.Combine("runtimes", "de", "a.resources.dll");
            task.RuntimePackAssets.FirstOrDefault().ItemSpec.Should().Contain(expectedResource);
        }

        [Fact]
        public void It_detects_transitive_framework_reference_with_no_runtime_pack()
        {
            // Scenario: AddTransitiveFrameworkReferences added a transitive reference after
            // ProcessFrameworkReferences ran, so there's no RuntimePack or UnavailableRuntimePack
            // for it.  ResolveRuntimePackAssets should detect this and produce NETSDK1235.

            var buildEngine = new MockBuildEngine();

            var task = new ResolveRuntimePackAssets()
            {
                BuildEngine = buildEngine,
                RuntimeIdentifier = "linux-x64",
                FrameworkReferences = new TaskItem[]
                {
                    new TaskItem("Microsoft.NETCore.App"),
                    new TaskItem("Microsoft.AspNetCore.App", new Dictionary<string, string>
                    {
                        ["IsTransitiveFrameworkReference"] = "true"
                    })
                },
                // Only NETCore.App has a resolved runtime pack — AspNetCore does not
                ResolvedRuntimePacks = Array.Empty<TaskItem>(),
                UnavailableRuntimePacks = Array.Empty<TaskItem>(),
                // RuntimeFrameworks produced by PFR for all KnownFrameworkReferences.
                // Non-profile frameworks have FrameworkName == ItemSpec.
                RuntimeFrameworks = new TaskItem[]
                {
                    new TaskItem("Microsoft.NETCore.App", new Dictionary<string, string>
                    {
                        [MetadataKeys.FrameworkName] = "Microsoft.NETCore.App"
                    }),
                    new TaskItem("Microsoft.AspNetCore.App", new Dictionary<string, string>
                    {
                        [MetadataKeys.FrameworkName] = "Microsoft.AspNetCore.App"
                    })
                }
            };

            task.Execute().Should().BeFalse("should fail due to missing transitive runtime pack");

            buildEngine.Errors.Should().ContainSingle(e =>
                e.Code == "NETSDK1235" && e.Message.Contains("Microsoft.AspNetCore.App"),
                "should report NETSDK1235 for the transitive AspNetCore framework reference");
        }

        [Fact]
        public void It_does_not_flag_profile_framework_references_as_missing()
        {
            // Profile framework references (e.g. Microsoft.WindowsDesktop.App.WindowsForms)
            // share their parent's runtime pack and should not be flagged as missing.

            var buildEngine = new MockBuildEngine();

            var task = new ResolveRuntimePackAssets()
            {
                BuildEngine = buildEngine,
                RuntimeIdentifier = "win-x64",
                FrameworkReferences = new TaskItem[]
                {
                    new TaskItem("Microsoft.NETCore.App"),
                    new TaskItem("Microsoft.WindowsDesktop.App.WindowsForms", new Dictionary<string, string>
                    {
                        ["IsTransitiveFrameworkReference"] = "true"
                    })
                },
                ResolvedRuntimePacks = Array.Empty<TaskItem>(),
                UnavailableRuntimePacks = Array.Empty<TaskItem>(),
                RuntimeFrameworks = new TaskItem[]
                {
                    new TaskItem("Microsoft.NETCore.App", new Dictionary<string, string>
                    {
                        [MetadataKeys.FrameworkName] = "Microsoft.NETCore.App"
                    }),
                    // Profile: FrameworkName != ItemSpec
                    new TaskItem("Microsoft.WindowsDesktop.App", new Dictionary<string, string>
                    {
                        [MetadataKeys.FrameworkName] = "Microsoft.WindowsDesktop.App.WindowsForms"
                    })
                }
            };

            task.Execute().Should().BeTrue("profile framework references should not produce errors");

            buildEngine.Errors.Should().BeEmpty(
                "WindowsForms is a profile — it shares WindowsDesktop.App's runtime pack");
        }
    }
}
