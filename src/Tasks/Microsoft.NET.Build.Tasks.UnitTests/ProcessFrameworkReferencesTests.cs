// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.NET.TestFramework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class ProcessFrameworkReferencesTests
    {
        // Shared runtime graph templates
        private const string MinimalRuntimeGraph = """
            {
                "runtimes": {
                    "any": {
                        "#import": ["base"]
                    },
                    "base": {
                        "#import": []
                    }
                }
            }
            """;
        
        private const string WindowsRuntimeGraph = """
            {
                "runtimes": {
                    "any": {
                        "#import": ["base"]
                    },
                    "base": {
                        "#import": []
                    },
                    "win": {
                        "#import": ["any"]
                    },
                    "win-x64": {
                        "#import": ["win"]
                    }
                }
            }
            """;
        
        private const string MultiPlatformRuntimeGraph = """
            {
                "runtimes": {
                    "any": {
                        "#import": ["base"]
                    },
                    "base": {
                        "#import": []
                    },
                    "win": {
                        "#import": ["any"]
                    },
                    "win-x64": {
                        "#import": ["win"]
                    },
                    "win-x86": {
                        "#import": ["win"]
                    },
                    "linux-x64": {
                        "#import": ["any"]
                    },
                    "osx": {
                        "#import": ["any"]
                    },
                    "osx-arm64": {
                        "#import": ["osx"]
                    },
                    "osx-x64": {
                        "#import": ["osx"]
                    }
                }
            }
            """;

        // Shared known framework references
        private readonly MockTaskItem _validWindowsSDKKnownFrameworkReference = CreateKnownFrameworkReference(
            "Microsoft.Windows.SDK.NET.Ref", "net5.0-windows10.0.18362", "10.0.18362.1-preview", 
            additionalMetadata: new Dictionary<string, string> {
                {MetadataKeys.RuntimePackAlwaysCopyLocal, "true"},
                {"IsWindowsOnly", "true"},
                {"RuntimePackRuntimeIdentifiers", "any"},
                {"RuntimePackNamePatterns", "Microsoft.Windows.SDK.NET.Ref"}
            });

        private readonly MockTaskItem _netcoreAppKnownFrameworkReference = CreateKnownFrameworkReference(
            "Microsoft.NETCore.App", "net5.0", "5.0.0-preview.4.20251.6", 
            runtimePackPattern: "Microsoft.NETCore.App.Runtime.**RID**", 
            runtimeIdentifiers: "win-x64");

        // Helper methods for creating common test objects
        private static MockTaskItem CreateKnownFrameworkReference(string name, string targetFramework, string version, 
            string? runtimePackPattern = null, string? runtimeIdentifiers = null, Dictionary<string, string>? additionalMetadata = null)
        {
            var metadata = new Dictionary<string, string>
            {
                {"TargetFramework", targetFramework},
                {"RuntimeFrameworkName", name},
                {"DefaultRuntimeFrameworkVersion", version},
                {"LatestRuntimeFrameworkVersion", version},
                {"TargetingPackName", name.EndsWith(".Ref") ? name : $"{name}.Ref"},
                {"TargetingPackVersion", version}
            };
            
            if (runtimePackPattern != null) metadata["RuntimePackNamePatterns"] = runtimePackPattern;
            if (runtimeIdentifiers != null) metadata["RuntimePackRuntimeIdentifiers"] = runtimeIdentifiers;
            if (additionalMetadata != null)
            {
                foreach (var kvp in additionalMetadata)
                    metadata[kvp.Key] = kvp.Value;
            }
            
            return new MockTaskItem(name, metadata);
        }
        
        private static MockTaskItem CreateKnownILCompilerPack(string targetFramework, string version, string runtimeIdentifiers)
        {
            return new MockTaskItem("Microsoft.DotNet.ILCompiler", new Dictionary<string, string>
            {
                ["TargetFramework"] = targetFramework,
                ["ILCompilerPackNamePattern"] = "runtime.**RID**.Microsoft.DotNet.ILCompiler",
                ["ILCompilerPackVersion"] = version,
                ["ILCompilerRuntimeIdentifiers"] = runtimeIdentifiers
            });
        }
        
        private static string CreateRuntimeGraphFile(string content)
        {
            var path = Path.GetTempFileName();
            File.WriteAllText(path, content);
            return path;
        }
        
        private static ProcessFrameworkReferences CreateTask(TaskConfiguration config)
        {
            var task = new ProcessFrameworkReferences
            {
                BuildEngine = config.UseCachingEngine ? new MockBuildEngine() : new MockNeverCacheBuildEngine4(),
                EnableTargetingPackDownload = config.EnableTargetingPackDownload,
                EnableRuntimePackDownload = config.EnableRuntimePackDownload,
                TargetFrameworkIdentifier = config.TargetFrameworkIdentifier ?? ".NETCoreApp",
                TargetFrameworkVersion = config.TargetFrameworkVersion ?? "5.0",
                NETCoreSdkRuntimeIdentifier = config.NETCoreSdkRuntimeIdentifier ?? "win-x64",
                RuntimeIdentifier = config.RuntimeIdentifier,
                SelfContained = config.SelfContained,
                TargetLatestRuntimePatch = config.TargetLatestRuntimePatch,
                TargetLatestRuntimePatchIsDefault = config.TargetLatestRuntimePatchIsDefault,
            };
            
            // Set nullable properties conditionally to avoid warnings
            if (config.TargetPlatformIdentifier != null) task.TargetPlatformIdentifier = config.TargetPlatformIdentifier;
            if (config.TargetPlatformVersion != null) task.TargetPlatformVersion = config.TargetPlatformVersion;
            if (config.RuntimeGraphPath != null) task.RuntimeGraphPath = config.RuntimeGraphPath;
            if (config.FrameworkReferences != null) task.FrameworkReferences = config.FrameworkReferences;
            if (config.KnownFrameworkReferences != null) task.KnownFrameworkReferences = config.KnownFrameworkReferences;
            if (config.KnownRuntimePacks != null) task.KnownRuntimePacks = config.KnownRuntimePacks;
            if (config.KnownILLinkPacks != null) task.KnownILLinkPacks = config.KnownILLinkPacks;
            if (config.KnownILCompilerPacks != null) task.KnownILCompilerPacks = config.KnownILCompilerPacks;
            
            // Set additional AOT/trimming properties
            if (config.PublishAot.HasValue) task.PublishAot = config.PublishAot.Value;
            if (config.PublishTrimmed.HasValue) task.PublishTrimmed = config.PublishTrimmed.Value;
            if (config.RequiresILLinkPack.HasValue) task.RequiresILLinkPack = config.RequiresILLinkPack.Value;
            if (config.EnableAotAnalyzer.HasValue) task.EnableAotAnalyzer = config.EnableAotAnalyzer.Value;
            if (config.EnableTrimAnalyzer.HasValue) task.EnableTrimAnalyzer = config.EnableTrimAnalyzer.Value;
            if (config.EnableSingleFileAnalyzer.HasValue) task.EnableSingleFileAnalyzer = config.EnableSingleFileAnalyzer.Value;
            
            if (!string.IsNullOrEmpty(config.NetCoreRoot)) task.NetCoreRoot = config.NetCoreRoot;
            if (!string.IsNullOrEmpty(config.NETCoreSdkVersion)) task.NETCoreSdkVersion = config.NETCoreSdkVersion;
            if (!string.IsNullOrEmpty(config.NETCoreSdkPortableRuntimeIdentifier)) task.NETCoreSdkPortableRuntimeIdentifier = config.NETCoreSdkPortableRuntimeIdentifier;
            
            // Missing assignment that was causing the issue
            task.RuntimeIdentifiers = config.RuntimeIdentifiers;
            
            return task;
        }
        
        private class TaskConfiguration
        {
            public bool UseCachingEngine { get; set; }
            public bool EnableTargetingPackDownload { get; set; } = true;
            public bool EnableRuntimePackDownload { get; set; }
            public string? TargetFrameworkIdentifier { get; set; }
            public string? TargetFrameworkVersion { get; set; }
            public string? TargetPlatformIdentifier { get; set; }
            public string? TargetPlatformVersion { get; set; }
            public string? NETCoreSdkRuntimeIdentifier { get; set; }
            public string? RuntimeIdentifier { get; set; }
            public string[]? RuntimeIdentifiers { get; set; }
            public bool SelfContained { get; set; }
            public string? RuntimeGraphPath { get; set; }
            public bool TargetLatestRuntimePatch { get; set; }
            public bool TargetLatestRuntimePatchIsDefault { get; set; }
            public MockTaskItem[]? FrameworkReferences { get; set; }
            public MockTaskItem[]? KnownFrameworkReferences { get; set; }
            public MockTaskItem[]? KnownRuntimePacks { get; set; }
            public MockTaskItem[]? KnownILLinkPacks { get; set; }
            public MockTaskItem[]? KnownILCompilerPacks { get; set; }
            public bool? PublishAot { get; set; }
            public bool? PublishTrimmed { get; set; }
            public bool? RequiresILLinkPack { get; set; }
            public bool? EnableAotAnalyzer { get; set; }
            public bool? EnableTrimAnalyzer { get; set; }
            public bool? EnableSingleFileAnalyzer { get; set; }
            public string? NetCoreRoot { get; set; }
            public string? NETCoreSdkVersion { get; set; }
            public string? NETCoreSdkPortableRuntimeIdentifier { get; set; }
        }

        [Theory]
        [InlineData(false)] // Without target platform
        [InlineData(true)]  // With target platform
        public void It_resolves_AspNetCore_FrameworkReferences(bool withTargetPlatform)
        {
            var aspNetCoreRef = CreateKnownFrameworkReference("Microsoft.AspNetCore.App", 
                ToolsetInfo.CurrentTargetFramework, "1.9.5", additionalMetadata: new Dictionary<string, string> 
                {
                    {"LatestRuntimeFrameworkVersion", "1.9.6"},
                    {"TargetingPackVersion", "1.9.0"}
                });
            
            var config = new TaskConfiguration
            {
                UseCachingEngine = true,
                TargetFrameworkVersion = ToolsetInfo.CurrentTargetFrameworkVersion,
                TargetPlatformIdentifier = withTargetPlatform ? "Windows" : null,
                TargetPlatformVersion = withTargetPlatform ? "10.0.18362" : null,
                FrameworkReferences = new[] { new MockTaskItem("Microsoft.AspNetCore.App", new Dictionary<string, string>()) },
                KnownFrameworkReferences = new[] { aspNetCoreRef }
            };
            
            var task = CreateTask(config);
            task.Execute().Should().BeTrue();
            
            task.PackagesToDownload.Should().NotBeNull().And.HaveCount(1);
            task.RuntimeFrameworks.Should().NotBeNull().And.HaveCount(1);
            task.RuntimeFrameworks[0].ItemSpec.Should().Be("Microsoft.AspNetCore.App");
            task.RuntimeFrameworks[0].GetMetadata(MetadataKeys.Version).Should().Be("1.9.5");
        }

        [Fact]
        public void It_does_not_resolve_FrameworkReferences_if_targetframework_doesnt_match()
        {
            var aspNetCoreRef = CreateKnownFrameworkReference("Microsoft.AspNetCore.App", "netcoreapp3.0", "1.9.5");
            
            var config = new TaskConfiguration
            {
                UseCachingEngine = true,
                EnableTargetingPackDownload = false, // Explicitly disable to test non-resolution
                TargetFrameworkVersion = "2.0", // Mismatched version
                FrameworkReferences = new[] { new MockTaskItem("Microsoft.AspNetCore.App", new Dictionary<string, string>()) },
                KnownFrameworkReferences = new[] { aspNetCoreRef }
            };
            
            var task = CreateTask(config);
            task.Execute().Should().BeTrue();
            
            task.PackagesToDownload.Should().BeNull();
            task.RuntimeFrameworks.Should().BeNull();
        }

        [Fact]
        public void Given_KnownFrameworkReferences_with_RuntimePackAlwaysCopyLocal_It_resolves_FrameworkReferences()
        {
            var windowsSDKRef = CreateKnownFrameworkReference("Microsoft.Windows.SDK.NET.Ref", 
                "net5.0-windows10.0.17760", "10.0.17760.1-preview", 
                additionalMetadata: new Dictionary<string, string> {
                    {MetadataKeys.RuntimePackAlwaysCopyLocal, "true"},
                    {"IsWindowsOnly", "true"},
                    {"RuntimePackRuntimeIdentifiers", "any"},
                    {"RuntimePackNamePatterns", "Microsoft.Windows.SDK.NET.Ref"}
                });
                
            var config = new TaskConfiguration
            {
                EnableRuntimePackDownload = true,
                TargetPlatformIdentifier = "Windows",
                TargetPlatformVersion = "10.0.18362",
                RuntimeGraphPath = CreateRuntimeGraphFile(MinimalRuntimeGraph),
                FrameworkReferences = new[] { new MockTaskItem("Microsoft.Windows.SDK.NET.Ref", new Dictionary<string, string>()) },
                KnownFrameworkReferences = new[] { windowsSDKRef, _validWindowsSDKKnownFrameworkReference }
            };
            
            var task = CreateTask(config);
            
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                task.Execute().Should().BeFalse("IsWindowsOnly=true");
                return;
            }

            task.Execute().Should().BeTrue();
            task.PackagesToDownload.Should().NotBeNull().And.HaveCount(1);
            task.RuntimeFrameworks.Should().BeNullOrEmpty("Should not contain RuntimePackAlwaysCopyLocal framework");
            
            // Validate targeting pack
            task.TargetingPacks.Should().NotBeNull().And.HaveCount(1);
            var targetingPack = task.TargetingPacks[0];
            targetingPack.ItemSpec.Should().Be("Microsoft.Windows.SDK.NET.Ref");
            targetingPack.GetMetadata(MetadataKeys.NuGetPackageId).Should().Be("Microsoft.Windows.SDK.NET.Ref");
            targetingPack.GetMetadata(MetadataKeys.NuGetPackageVersion).Should().Be("10.0.18362.1-preview");
            
            // Validate runtime pack
            task.RuntimePacks.Should().NotBeNull().And.HaveCount(1);
            var runtimePack = task.RuntimePacks[0];
            runtimePack.ItemSpec.Should().Be("Microsoft.Windows.SDK.NET.Ref");
            runtimePack.GetMetadata(MetadataKeys.RuntimePackAlwaysCopyLocal).Should().Be("true");
        }

        [Fact]
        public void It_resolves_self_contained_FrameworkReferences_to_download()
        {
            var config = new TaskConfiguration
            {
                EnableRuntimePackDownload = true,
                TargetPlatformIdentifier = "Windows",
                TargetPlatformVersion = "10.0.18362",
                RuntimeIdentifier = "win-x64",
                SelfContained = true,
                TargetLatestRuntimePatch = true,
                TargetLatestRuntimePatchIsDefault = true,
                RuntimeGraphPath = CreateRuntimeGraphFile(WindowsRuntimeGraph),
                FrameworkReferences = new[] {
                    new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>()),
                    new MockTaskItem("Microsoft.Windows.SDK.NET.Ref", new Dictionary<string, string>())
                },
                KnownFrameworkReferences = new[] { _netcoreAppKnownFrameworkReference, _validWindowsSDKKnownFrameworkReference }
            };
            
            var task = CreateTask(config);
            
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                task.Execute().Should().BeTrue();
                task.PackagesToDownload.Should().NotBeNull().And.HaveCount(3);
                task.PackagesToDownload.Should().Contain(p => p.ItemSpec == "Microsoft.Windows.SDK.NET.Ref");
                task.PackagesToDownload.Should().Contain(p => p.ItemSpec == "Microsoft.NETCore.App.Ref");
                task.PackagesToDownload.Should().Contain(p => p.ItemSpec == "Microsoft.NETCore.App.Runtime.win-x64");
            }
            else
            {
                task.Execute().Should().BeFalse("IsWindowsOnly=true");
            }
        }

        [Fact]
        public void Given_reference_to_NETCoreApp_It_should_not_resolve_runtime_pack()
        {
            var config = new TaskConfiguration
            {
                EnableRuntimePackDownload = true,
                TargetPlatformIdentifier = "Windows",
                TargetPlatformVersion = "10.0.18362",
                TargetLatestRuntimePatch = true,
                TargetLatestRuntimePatchIsDefault = true,
                RuntimeGraphPath = CreateRuntimeGraphFile(WindowsRuntimeGraph),
                FrameworkReferences = new[] {
                    new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>()),
                    new MockTaskItem("Microsoft.Windows.SDK.NET.Ref", new Dictionary<string, string>())
                },
                KnownFrameworkReferences = new[] { _netcoreAppKnownFrameworkReference, _validWindowsSDKKnownFrameworkReference }
            };
            
            var task = CreateTask(config);
            
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                task.Execute().Should().BeFalse("IsWindowsOnly=true");
                return;
            }
            
            task.Execute().Should().BeTrue();
            task.TargetingPacks.Should().NotBeNull().And.HaveCount(2);
            task.TargetingPacks.Should().Contain(p => p.GetMetadata(MetadataKeys.NuGetPackageId) == "Microsoft.Windows.SDK.NET.Ref");
            task.TargetingPacks.Should().Contain(p => p.GetMetadata(MetadataKeys.NuGetPackageId) == "Microsoft.NETCore.App.Ref");
            task.RuntimePacks.Should().NotBeNull().And.HaveCount(1);
            task.RuntimePacks[0].ItemSpec.Should().Be("Microsoft.Windows.SDK.NET.Ref", "should not resolve runtime pack for Microsoft.NETCore.App");
        }

        [Theory]
        [InlineData(null, new[] { "win-x64", "win-x86", "linux-x64" }, "Multiple RuntimeIdentifiers without RuntimeIdentifier")]
        [InlineData("", new[] { "win-x64", "linux-x64" }, "Empty RuntimeIdentifier with RuntimeIdentifiers")]
        [InlineData("any", new[] { "win-x64" }, "Any RID with RuntimeIdentifiers")]
        [InlineData(null, new string[0], "No RuntimeIdentifier with empty RuntimeIdentifiers")]
        [InlineData(null, null, "No RuntimeIdentifier with null RuntimeIdentifiers")]
        public void It_processes_various_RuntimeIdentifier_scenarios(string? runtimeIdentifier, string[]? runtimeIdentifiers, string scenario)
        {
            var netCoreAppRef = CreateKnownFrameworkReference("Microsoft.NETCore.App", "net5.0", "5.0.0",
                "Microsoft.NETCore.App.Runtime.**RID**", "win-x64;win-x86;linux-x64");
                
            var config = new TaskConfiguration
            {
                EnableRuntimePackDownload = true,
                TargetLatestRuntimePatch = true,
                RuntimeIdentifier = runtimeIdentifier,
                RuntimeIdentifiers = runtimeIdentifiers,
                RuntimeGraphPath = CreateRuntimeGraphFile(MultiPlatformRuntimeGraph),
                FrameworkReferences = new[] { new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>()) },
                KnownFrameworkReferences = new[] { netCoreAppRef }
            };
            
            var task = CreateTask(config);
            task.Execute().Should().BeTrue($"Task should succeed for scenario: {scenario}");
            
            task.PackagesToDownload.Should().NotBeNull();
            task.PackagesToDownload.Should().Contain(p => p.ItemSpec == "Microsoft.NETCore.App.Ref", $"Should contain targeting pack for scenario: {scenario}");
            
            // Validate expected runtime packs based on scenario
            if (runtimeIdentifiers != null && runtimeIdentifiers.Length > 0)
            {
                foreach (var rid in runtimeIdentifiers)
                {
                    task.PackagesToDownload.Should().Contain(p => p.ItemSpec == $"Microsoft.NETCore.App.Runtime.{rid}", 
                        $"Should contain runtime pack for {rid} in scenario: {scenario}");
                }
            }
        }

        // This test is now covered by the theory above

        // This test is now covered by the theory above

        [Fact]
        public void It_processes_RuntimeIdentifiers_with_AlwaysCopyLocal_and_no_RuntimeIdentifier()
        {
            var config = new TaskConfiguration
            {
                EnableRuntimePackDownload = true,
                TargetPlatformIdentifier = "Windows",
                TargetPlatformVersion = "10.0.18362",
                RuntimeIdentifier = null,
                RuntimeIdentifiers = new[] { "win-x64" },
                RuntimeGraphPath = CreateRuntimeGraphFile(WindowsRuntimeGraph),
                FrameworkReferences = new[] { new MockTaskItem("Microsoft.Windows.SDK.NET.Ref", new Dictionary<string, string>()) },
                KnownFrameworkReferences = new[] { _validWindowsSDKKnownFrameworkReference }
            };
            
            var task = CreateTask(config);
            
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                task.Execute().Should().BeFalse("IsWindowsOnly=true");
                return;
            }

            task.Execute().Should().BeTrue();
            task.TargetingPacks.Should().NotBeNull().And.HaveCount(1);
            task.RuntimePacks.Should().NotBeNull().And.HaveCount(1);
            task.RuntimePacks[0].ItemSpec.Should().Be("Microsoft.Windows.SDK.NET.Ref");
        }

        // This test is now covered by the theory above

        // This test is now covered by the theory above

        [Fact]
        public void It_handles_real_world_ridless_scenario_with_aot_and_trimming()
        {
            // This test reproduces the exact scenario from the reported issue
            var netCoreAppRef = CreateKnownFrameworkReference("Microsoft.NETCore.App", "net10.0", "10.0.0-rc.2.25457.102",
                "Microsoft.NETCore.App.Runtime.**RID**", "linux-arm;linux-arm64;osx-arm64;osx-x64;win-x64;win-x86");
                
            var macOSRef = CreateKnownFrameworkReference("Microsoft.macOS", "net10.0", "15.5.10834-ci.darc-net10-0-3e2d6574-3e2e-4233-aab9-99cf75de33e4",
                "Microsoft.macOS.Runtime.osx.net10.0_15.5", "osx-x64;osx-arm64", 
                new Dictionary<string, string> {
                    ["TargetingPackName"] = "Microsoft.macOS.Ref.net10.0_15.5",
                    ["Profile"] = "macOS"
                });
                
            var nativeAOTRuntimePack = new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>
            {
                ["TargetFramework"] = "net10.0",
                ["RuntimeFrameworkName"] = "Microsoft.NETCore.App",
                ["LatestRuntimeFrameworkVersion"] = "10.0.0-rc.2.25457.102",
                ["RuntimePackNamePatterns"] = "Microsoft.NETCore.App.Runtime.NativeAOT.**RID**",
                ["RuntimePackRuntimeIdentifiers"] = "osx-arm64;osx-x64;win-x64;linux-x64",
                ["RuntimePackLabels"] = "NativeAOT"
            });
            
            var ilLinkPack = new MockTaskItem("Microsoft.NET.ILLink.Tasks", new Dictionary<string, string>
            {
                ["TargetFramework"] = "net10.0",
                ["ILLinkPackVersion"] = "10.0.0-rc.2.25457.102"
            });
            
            var ilCompilerPack = new MockTaskItem("Microsoft.DotNet.ILCompiler", new Dictionary<string, string>
            {
                ["TargetFramework"] = "net10.0",
                ["ILCompilerPackNamePattern"] = "runtime.**RID**.Microsoft.DotNet.ILCompiler",
                ["ILCompilerPackVersion"] = "10.0.0-rc.2.25457.102",
                ["ILCompilerRuntimeIdentifiers"] = "osx-x64;osx-arm64;win-x64;linux-x64"
            });

            var config = new TaskConfiguration
            {
                TargetFrameworkVersion = "10.0",
                TargetPlatformIdentifier = "macOS",
                TargetPlatformVersion = "15.5",
                EnableRuntimePackDownload = true,
                RequiresILLinkPack = true,
                PublishAot = true,
                PublishTrimmed = true,
                EnableAotAnalyzer = true,
                EnableTrimAnalyzer = true,
                EnableSingleFileAnalyzer = true,
                NETCoreSdkRuntimeIdentifier = "osx-arm64",
                NETCoreSdkPortableRuntimeIdentifier = "osx-arm64",
                RuntimeIdentifier = null, // Key: No primary RuntimeIdentifier
                RuntimeIdentifiers = new[] { "osx-arm64", "osx-x64" }, // But has RuntimeIdentifiers
                RuntimeGraphPath = CreateRuntimeGraphFile(MultiPlatformRuntimeGraph),
                NetCoreRoot = "/usr/local/share/dotnet",
                NETCoreSdkVersion = "10.0.100-rc.2.25457.102",
                FrameworkReferences = new[] {
                    new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string> { ["IsImplicitlyDefined"] = "true" }),
                    new MockTaskItem("Microsoft.macOS", new Dictionary<string, string> { ["IsImplicitlyDefined"] = "true" })
                },
                KnownFrameworkReferences = new[] { netCoreAppRef, macOSRef },
                KnownRuntimePacks = new[] { nativeAOTRuntimePack },
                KnownILLinkPacks = new[] { ilLinkPack },
                KnownILCompilerPacks = new[] { ilCompilerPack }
            };
            
            var task = CreateTask(config);
            
            // Key validation: The task should complete successfully without crashing
            // This was the main issue - null RuntimeIdentifier with RuntimeIdentifiers would crash
            task.Execute().Should().BeTrue("Task should not crash with null RuntimeIdentifier but present RuntimeIdentifiers");
            
            // Validate that frameworks are processed correctly
            task.TargetingPacks.Should().NotBeNull();
            task.TargetingPacks.Should().Contain(p => p.GetMetadata(MetadataKeys.NuGetPackageId) == "Microsoft.NETCore.App.Ref");
            task.TargetingPacks.Should().Contain(p => p.GetMetadata(MetadataKeys.NuGetPackageId) == "Microsoft.macOS.Ref.net10.0_15.5");
            
            // Validate that AOT tooling is processed
            task.PackagesToDownload.Should().NotBeNull();
            task.PackagesToDownload.Should().Contain(p => p.ItemSpec.Contains("Microsoft.DotNet.ILCompiler"), "Should include AOT compiler tooling");
        }

        [Fact]
        public void It_handles_AOT_properties_without_failure()
        {
            var config = new TaskConfiguration
            {
                TargetFrameworkVersion = "8.0",
                SelfContained = true,
                RuntimeIdentifier = "linux-x64",
                EnableRuntimePackDownload = true,
                NETCoreSdkRuntimeIdentifier = "linux-x64",
                RuntimeGraphPath = CreateRuntimeGraphFile(MultiPlatformRuntimeGraph),
                FrameworkReferences = new[] { new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>()) },
                KnownFrameworkReferences = new[] { 
                    CreateKnownFrameworkReference("Microsoft.NETCore.App", "net8.0", "8.0.0", "Microsoft.NETCore.App.Runtime.**RID**", "linux-x64")
                },
                KnownILCompilerPacks = new[] {
                    CreateKnownILCompilerPack("net8.0", "8.0.0", "linux-x64;win-x64;osx-x64;osx-arm64")
                },
                PublishAot = true
            };
            
            var task = CreateTask(config);
            
            task.Execute().Should().BeTrue("Task should handle AOT properties without failing");
            
            task.PackagesToDownload.Should().NotBeNull();
            task.PackagesToDownload.Should().Contain(p => p.ItemSpec == "Microsoft.NETCore.App.Runtime.linux-x64");
        }

        [Fact]
        public void It_handles_trimming_scenarios()
        {
            var config = new TaskConfiguration
            {
                TargetFrameworkVersion = "8.0",
                PublishTrimmed = true,
                EnableTrimAnalyzer = true,
                SelfContained = true,
                RuntimeIdentifier = "win-x64",
                EnableRuntimePackDownload = true,
                RuntimeGraphPath = CreateRuntimeGraphFile(WindowsRuntimeGraph),
                FrameworkReferences = new[] { new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>()) },
                KnownFrameworkReferences = new[] { 
                    CreateKnownFrameworkReference("Microsoft.NETCore.App", "net8.0", "8.0.0", "Microsoft.NETCore.App.Runtime.**RID**", "win-x64")
                }
            };
            
            var task = CreateTask(config);
            task.FirstTargetFrameworkVersionToSupportTrimAnalyzer = "6.0";
            
            task.Execute().Should().BeTrue("Trimming scenario should be handled");
            
            task.PackagesToDownload.Should().NotBeNull();
            task.PackagesToDownload.Should().Contain(p => p.ItemSpec == "Microsoft.NETCore.App.Runtime.win-x64");
        }

        [Fact]
        public void It_handles_combined_publish_properties()
        {
            var config = new TaskConfiguration
            {
                TargetFrameworkVersion = "8.0",
                SelfContained = true,
                RuntimeIdentifier = "osx-arm64",
                EnableRuntimePackDownload = true,
                NETCoreSdkRuntimeIdentifier = "osx-arm64",
                RuntimeGraphPath = CreateRuntimeGraphFile(MultiPlatformRuntimeGraph),
                FrameworkReferences = new[] { new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>()) },
                KnownFrameworkReferences = new[] { 
                    CreateKnownFrameworkReference("Microsoft.NETCore.App", "net8.0", "8.0.0", "Microsoft.NETCore.App.Runtime.**RID**", "osx-arm64")
                },
                KnownILCompilerPacks = new[] {
                    CreateKnownILCompilerPack("net8.0", "8.0.0", "linux-x64;win-x64;osx-x64;osx-arm64")
                },
                PublishAot = true,
                PublishTrimmed = true
            };
            
            var task = CreateTask(config);
            
            task.Execute().Should().BeTrue("Combined publish properties should be handled");
            
            task.PackagesToDownload.Should().NotBeNull();
            task.PackagesToDownload.Should().Contain(p => p.ItemSpec == "Microsoft.NETCore.App.Runtime.osx-arm64");
        }

        [Fact]
        public void It_handles_mobile_iOS_framework_references()
        {
            // Create compatible framework references with correct target framework
            var netCoreRef = CreateKnownFrameworkReference("Microsoft.NETCore.App", "net8.0", "8.0.0", "Microsoft.NETCore.App.Runtime.**RID**", "ios-arm64");
            var iosFrameworkRef = CreateKnownFrameworkReference("Microsoft.iOS", "net8.0-ios17.0", "17.0.8478",
                "Microsoft.iOS.Runtime.**RID**", "ios-arm64;iossimulator-x64;iossimulator-arm64",
                new Dictionary<string, string> { ["Profile"] = "iOS" });
                
            var config = new TaskConfiguration
            {
                TargetFrameworkVersion = "8.0",
                TargetPlatformIdentifier = "iOS",
                TargetPlatformVersion = "17.0",
                EnableRuntimePackDownload = true,
                RuntimeIdentifier = "ios-arm64",
                RuntimeGraphPath = CreateRuntimeGraphFile(MultiPlatformRuntimeGraph),
                FrameworkReferences = new[] { 
                    new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>()),
                    new MockTaskItem("Microsoft.iOS", new Dictionary<string, string>())
                },
                KnownFrameworkReferences = new[] { netCoreRef, iosFrameworkRef }
            };
            
            var task = CreateTask(config);
            task.Execute().Should().BeTrue();
            
            // Should include both .NET and iOS targeting packs
            task.TargetingPacks.Should().NotBeNull();
            task.TargetingPacks.Should().Contain(p => p.GetMetadata(MetadataKeys.NuGetPackageId) == "Microsoft.NETCore.App.Ref");
            task.TargetingPacks.Should().Contain(p => p.GetMetadata(MetadataKeys.NuGetPackageId) == "Microsoft.iOS.Ref");
        }

        [Theory]
        [InlineData(true, true, true, "All analyzers enabled")]
        [InlineData(true, true, false, "AOT and Trim analyzers only")]
        [InlineData(false, false, true, "SingleFile analyzer only")]
        [InlineData(true, false, false, "AOT analyzer only")]
        public void It_handles_analyzer_combinations(bool aotAnalyzer, bool trimAnalyzer, bool singleFileAnalyzer, string scenario)
        {
            var config = new TaskConfiguration
            {
                TargetFrameworkVersion = "8.0", // Ensure analyzers are supported
                EnableTargetingPackDownload = true, // Need this for PackagesToDownload
                EnableAotAnalyzer = aotAnalyzer,
                EnableTrimAnalyzer = trimAnalyzer,
                EnableSingleFileAnalyzer = singleFileAnalyzer,
                RuntimeGraphPath = CreateRuntimeGraphFile(WindowsRuntimeGraph),
                FrameworkReferences = new[] { new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>()) },
                KnownFrameworkReferences = new[] { 
                    CreateKnownFrameworkReference("Microsoft.NETCore.App", "net8.0", "8.0.0", "Microsoft.NETCore.App.Runtime.**RID**", "win-x64")
                }
            };
            
            var task = CreateTask(config);
            // Set framework version thresholds for analyzer support
            task.FirstTargetFrameworkVersionToSupportAotAnalyzer = "7.0";
            task.FirstTargetFrameworkVersionToSupportTrimAnalyzer = "6.0";
            task.FirstTargetFrameworkVersionToSupportSingleFileAnalyzer = "6.0";
            
            task.Execute().Should().BeTrue($"Task should succeed for scenario: {scenario}");
            task.PackagesToDownload.Should().NotBeNull();
        }

        [Theory]
        [InlineData("Android", "34.0", "android-arm64")]
        [InlineData("macOS", "14.0", "osx-arm64")]
        [InlineData("Windows", "10.0.19041.0", "win-x64")]
        public void It_handles_different_target_platforms(string platformId, string platformVersion, string runtimeId)
        {
            var platformFrameworkRef = CreateKnownFrameworkReference($"Microsoft.{platformId}", "net8.0", "8.0.0",
                $"Microsoft.{platformId}.Runtime.**RID**", runtimeId,
                new Dictionary<string, string> { 
                    ["Profile"] = platformId,
                    ["IsWindowsOnly"] = (platformId == "Windows").ToString().ToLower()
                });
                
            var config = new TaskConfiguration
            {
                TargetFrameworkVersion = "8.0",
                TargetPlatformIdentifier = platformId,
                TargetPlatformVersion = platformVersion,
                EnableRuntimePackDownload = true,
                RuntimeIdentifier = runtimeId,
                RuntimeGraphPath = CreateRuntimeGraphFile(MultiPlatformRuntimeGraph),
                FrameworkReferences = new[] { 
                    new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>()),
                    new MockTaskItem($"Microsoft.{platformId}", new Dictionary<string, string>())
                },
                KnownFrameworkReferences = new[] { 
                    CreateKnownFrameworkReference("Microsoft.NETCore.App", "net8.0", "8.0.0", "Microsoft.NETCore.App.Runtime.**RID**", runtimeId),
                    platformFrameworkRef 
                }
            };
            
            var task = CreateTask(config);
            
            // Windows-only framework should fail on non-Windows
            if (platformId == "Windows" && Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                task.Execute().Should().BeFalse("Windows-only framework should fail on non-Windows platforms");
                return;
            }
            
            task.Execute().Should().BeTrue($"Should handle {platformId} platform targeting");
            task.TargetingPacks.Should().NotBeNull();
        }


        [Fact]
        public void It_handles_complex_cross_compilation_RuntimeIdentifiers()
        {
            var netCoreAppRef = CreateKnownFrameworkReference("Microsoft.NETCore.App", "net8.0", "8.0.0",
                "Microsoft.NETCore.App.Runtime.**RID**", 
                "linux-x64;linux-musl-x64;linux-arm64;linux-musl-arm64;alpine-x64;alpine-arm64");
                
            var config = new TaskConfiguration
            {
                TargetFrameworkVersion = "8.0",
                EnableRuntimePackDownload = true,
                RuntimeIdentifier = null, // No primary RID
                RuntimeIdentifiers = new[] { "linux-x64", "linux-musl-x64", "alpine-x64", "linux-arm64" }, // Mixed supported/unsupported
                RuntimeGraphPath = CreateRuntimeGraphFile(MultiPlatformRuntimeGraph),
                FrameworkReferences = new[] { new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>()) },
                KnownFrameworkReferences = new[] { netCoreAppRef }
            };
            
            var task = CreateTask(config);
            task.Execute().Should().BeTrue("Should handle mixed supported/unsupported RIDs gracefully");
            
            task.PackagesToDownload.Should().NotBeNull();
            task.PackagesToDownload.Should().Contain(p => p.ItemSpec == "Microsoft.NETCore.App.Ref");
            
            // Should download runtime packs for supported RIDs only
            var supportedRids = new[] { "linux-x64", "linux-musl-x64", "linux-arm64" };
            foreach (var rid in supportedRids)
            {
                task.PackagesToDownload.Should().Contain(p => p.ItemSpec == $"Microsoft.NETCore.App.Runtime.{rid}",
                    $"Should include runtime pack for supported RID: {rid}");
            }
        }
    }
}
