// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.NET.TestFramework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class ProcessFrameworkReferencesTests
    {
        private MockTaskItem _validWindowsSDKKnownFrameworkReference
            = new("Microsoft.Windows.SDK.NET.Ref",
                new Dictionary<string, string>
                {
                    {"TargetFramework", "net5.0-windows10.0.18362"},
                    {"RuntimeFrameworkName", "Microsoft.Windows.SDK.NET.Ref"},
                    {"DefaultRuntimeFrameworkVersion", "10.0.18362.1-preview"},
                    {"LatestRuntimeFrameworkVersion", "10.0.18362.1-preview"},
                    {"TargetingPackName", "Microsoft.Windows.SDK.NET.Ref"},
                    {"TargetingPackVersion", "10.0.18362.1-preview"},
                    {"RuntimePackNamePatterns", "Microsoft.Windows.SDK.NET.Ref"},
                    {"RuntimePackRuntimeIdentifiers", "any"},
                    {MetadataKeys.RuntimePackAlwaysCopyLocal, "true"},
                    {"IsWindowsOnly", "true"},
                });


        private MockTaskItem _netcoreAppKnownFrameworkReference =
            new("Microsoft.NETCore.App",
                new Dictionary<string, string>
                {
                    {"TargetFramework", "net5.0"},
                    {"RuntimeFrameworkName", "Microsoft.NETCore.App"},
                    {"DefaultRuntimeFrameworkVersion", "5.0.0-preview.4.20251.6"},
                    {"LatestRuntimeFrameworkVersion", "5.0.0-preview.4.20251.6"},
                    {"TargetingPackName", "Microsoft.NETCore.App.Ref"},
                    {"TargetingPackVersion", "5.0.0-preview.4.20251.6"},
                    {"RuntimePackNamePatterns", "Microsoft.NETCore.App.Runtime.**RID**"},
                    {"RuntimePackRuntimeIdentifiers", "win-x64"},
                });

        [Fact]
        public void It_resolves_FrameworkReferences()
        {
            var task = new ProcessFrameworkReferences
            {
                BuildEngine = new MockBuildEngine(),
                EnableTargetingPackDownload = true,
                TargetFrameworkIdentifier = ".NETCoreApp",
                TargetFrameworkVersion = ToolsetInfo.CurrentTargetFrameworkVersion,
                FrameworkReferences = new[]
                {
                    new MockTaskItem("Microsoft.AspNetCore.App", new Dictionary<string, string>())
                },
                KnownFrameworkReferences = new[]
                {
                    new MockTaskItem("Microsoft.AspNetCore.App",
                        new Dictionary<string, string>()
                        {
                            {"TargetFramework", ToolsetInfo.CurrentTargetFramework},
                            {"RuntimeFrameworkName", "Microsoft.AspNetCore.App"},
                            {"DefaultRuntimeFrameworkVersion", "1.9.5"},
                            {"LatestRuntimeFrameworkVersion", "1.9.6"},
                            {"TargetingPackName", "Microsoft.AspNetCore.App"},
                            {"TargetingPackVersion", "1.9.0"}
                        })
                }
            };

            task.Execute().Should().BeTrue();
            task.PackagesToDownload.Should().NotBeNull().And.HaveCount(1);
            task.RuntimeFrameworks.Should().NotBeNull().And.HaveCount(1);
            task.RuntimeFrameworks[0].ItemSpec.Should().Be("Microsoft.AspNetCore.App");
            task.RuntimeFrameworks[0].GetMetadata(MetadataKeys.Version).Should().Be("1.9.5");
        }

        [Fact]
        public void Given_targetPlatform_and_targetPlatform_version_It_resolves_FrameworkReferences_()
        {
            var task = new ProcessFrameworkReferences
            {
                BuildEngine = new MockBuildEngine(),
                EnableTargetingPackDownload = true,
                TargetFrameworkIdentifier = ".NETCoreApp",
                TargetFrameworkVersion = ToolsetInfo.CurrentTargetFrameworkVersion,
                TargetPlatformIdentifier = "Windows",
                TargetPlatformVersion = "10.0.18362",
                FrameworkReferences = new[]
                {
                    new MockTaskItem("Microsoft.AspNetCore.App", new Dictionary<string, string>())
                },
                KnownFrameworkReferences = new[]
                {
                    new MockTaskItem("Microsoft.AspNetCore.App",
                        new Dictionary<string, string>()
                        {
                            {"TargetFramework", ToolsetInfo.CurrentTargetFramework},
                            {"RuntimeFrameworkName", "Microsoft.AspNetCore.App"},
                            {"DefaultRuntimeFrameworkVersion", "1.9.5"},
                            {"LatestRuntimeFrameworkVersion", "1.9.6"},
                            {"TargetingPackName", "Microsoft.AspNetCore.App"},
                            {"TargetingPackVersion", "1.9.0"}
                        })
                }
            };

            task.Execute().Should().BeTrue();

            task.PackagesToDownload.Should().NotBeNull().And.HaveCount(1);
            task.RuntimeFrameworks.Should().NotBeNull().And.HaveCount(1);
            task.RuntimeFrameworks[0].ItemSpec.Should().Be("Microsoft.AspNetCore.App");
            task.RuntimeFrameworks[0].GetMetadata(MetadataKeys.Version).Should().Be("1.9.5");
        }

        [Fact]
        public void It_does_not_resolve_FrameworkReferences_if_targetframework_doesnt_match()
        {
            var task = new ProcessFrameworkReferences
            {
                BuildEngine = new MockBuildEngine(),
                TargetFrameworkIdentifier = ".NETCoreApp",
                TargetFrameworkVersion = "2.0",
                FrameworkReferences = new[]
                {
                    new MockTaskItem("Microsoft.AspNetCore.App", new Dictionary<string, string>())
                },
                KnownFrameworkReferences = new[]
                {
                    new MockTaskItem("Microsoft.AspNetCore.App",
                        new Dictionary<string, string>()
                        {
                            {"TargetFramework", "netcoreapp3.0"},
                            {"RuntimeFrameworkName", "Microsoft.AspNetCore.App"},
                            {"DefaultRuntimeFrameworkVersion", "1.9.5"},
                            {"LatestRuntimeFrameworkVersion", "1.9.6"},
                            {"TargetingPackName", "Microsoft.AspNetCore.App"},
                            {"TargetingPackVersion", "1.9.0"}
                        })
                }
            };

            task.Execute().Should().BeTrue();

            task.PackagesToDownload.Should().BeNull();
            task.RuntimeFrameworks.Should().BeNull();
        }

        [Fact]
        public void Given_KnownFrameworkReferences_with_RuntimePackAlwaysCopyLocal_It_resolves_FrameworkReferences()
        {
            const string minimalRuntimeGraphPathContent =
                "{\"runtimes\":{\"any\":{\"#import\":[\"base\"]},\"base\":{\"#import\":[]}}}";
            var runtimeGraphPathPath = Path.GetTempFileName();
            File.WriteAllText(runtimeGraphPathPath, minimalRuntimeGraphPathContent);

            var task = new ProcessFrameworkReferences
            {
                BuildEngine = new MockNeverCacheBuildEngine4(),
                EnableTargetingPackDownload = true,
                TargetFrameworkIdentifier = ".NETCoreApp",
                TargetFrameworkVersion = "5.0",
                TargetPlatformIdentifier = "Windows",
                TargetPlatformVersion = "10.0.18362",
                EnableRuntimePackDownload = true,
                RuntimeGraphPath =
                    runtimeGraphPathPath,
                FrameworkReferences =
                    new[] { new MockTaskItem("Microsoft.Windows.SDK.NET.Ref", new Dictionary<string, string>()) },
                KnownFrameworkReferences = new[]
                {
                    new MockTaskItem("Microsoft.Windows.SDK.NET.Ref",
                        new Dictionary<string, string>
                        {
                            {"TargetFramework", $"net5.0-windows10.0.17760"},
                            {"RuntimeFrameworkName", "Microsoft.Windows.SDK.NET.Ref"},
                            {"DefaultRuntimeFrameworkVersion", "10.0.17760.1-preview"},
                            {"LatestRuntimeFrameworkVersion", "10.0.17760.1-preview"},
                            {"TargetingPackName", "Microsoft.Windows.SDK.NET.Ref"},
                            {"TargetingPackVersion", "10.0.17760.1-preview"},
                            {"RuntimePackNamePatterns", "Microsoft.Windows.SDK.NET.Ref"},
                            {"RuntimePackRuntimeIdentifiers", "any"},
                            {MetadataKeys.RuntimePackAlwaysCopyLocal, "true"},
                            {"IsWindowsOnly", "true"},
                        }),
                    _validWindowsSDKKnownFrameworkReference,
                }
            };

            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                task.Execute().Should().BeFalse("IsWindowsOnly=true");
                return;
            }

            task.Execute().Should().BeTrue();

            task.PackagesToDownload.Should().NotBeNull().And.HaveCount(1);

            task.RuntimeFrameworks.Should().BeNullOrEmpty(
                "Should not contain RuntimePackAlwaysCopyLocal framework, or it will be put into runtimeconfig.json");

            task.TargetingPacks.Should().NotBeNull().And.HaveCount(1);
            task.TargetingPacks[0].ItemSpec.Should().Be("Microsoft.Windows.SDK.NET.Ref");
            task.TargetingPacks[0].GetMetadata(MetadataKeys.NuGetPackageId).Should()
                .Be("Microsoft.Windows.SDK.NET.Ref");
            task.TargetingPacks[0].GetMetadata(MetadataKeys.NuGetPackageVersion).Should().Be("10.0.18362.1-preview");
            task.TargetingPacks[0].GetMetadata(MetadataKeys.PackageConflictPreferredPackages).Should()
                .Be("Microsoft.Windows.SDK.NET.Ref");
            task.TargetingPacks[0].GetMetadata(MetadataKeys.RuntimeFrameworkName).Should()
                .Be("Microsoft.Windows.SDK.NET.Ref");
            task.TargetingPacks[0].GetMetadata(MetadataKeys.RuntimeIdentifier).Should().Be("");

            task.RuntimePacks.Should().NotBeNull().And.HaveCount(1);
            task.RuntimePacks[0].ItemSpec.Should().Be("Microsoft.Windows.SDK.NET.Ref");
            task.RuntimePacks[0].GetMetadata(MetadataKeys.FrameworkName).Should().Be("Microsoft.Windows.SDK.NET.Ref");
            task.RuntimePacks[0].GetMetadata(MetadataKeys.NuGetPackageVersion).Should().Be("10.0.18362.1-preview");
            task.RuntimePacks[0].GetMetadata(MetadataKeys.RuntimePackAlwaysCopyLocal).Should().Be("true");
        }

        [Fact]
        public void It_resolves_self_contained_FrameworkReferences_to_download()
        {
            const string minimalRuntimeGraphPathContent =
                "{\"runtimes\":{\"any\":{\"#import\":[\"base\"]},\"base\":{\"#import\":[]},\"win\":{\"#import\":[\"any\"]},\"win-x64\":{\"#import\":[\"win\"]}}}";
            var runtimeGraphPathPath = Path.GetTempFileName();
            File.WriteAllText(runtimeGraphPathPath, minimalRuntimeGraphPathContent);

            var task = new ProcessFrameworkReferences
            {
                BuildEngine = new MockNeverCacheBuildEngine4(),
                EnableTargetingPackDownload = true,
                TargetFrameworkIdentifier = ".NETCoreApp",
                TargetFrameworkVersion = "5.0",
                TargetPlatformIdentifier = "Windows",
                TargetPlatformVersion = "10.0.18362",
                NETCoreSdkRuntimeIdentifier = "win-x64",
                RuntimeIdentifier = "win-x64",
                RuntimeGraphPath =
                    runtimeGraphPathPath,
                SelfContained = true,
                TargetLatestRuntimePatch = true,
                TargetLatestRuntimePatchIsDefault = true,
                EnableRuntimePackDownload = true,
                FrameworkReferences =
                    new[]
                    {
                        new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>()),
                        new MockTaskItem("Microsoft.Windows.SDK.NET.Ref", new Dictionary<string, string>())
                    },
                KnownFrameworkReferences = new[]
                {
                    _netcoreAppKnownFrameworkReference, _validWindowsSDKKnownFrameworkReference,
                }
            };
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
            const string minimalRuntimeGraphPathContent =
                "{\"runtimes\":{\"any\":{\"#import\":[\"base\"]},\"base\":{\"#import\":[]},\"win\":{\"#import\":[\"any\"]},\"win-x64\":{\"#import\":[\"win\"]}}}";
            var runtimeGraphPathPath = Path.GetTempFileName();
            File.WriteAllText(runtimeGraphPathPath, minimalRuntimeGraphPathContent);

            var task = new ProcessFrameworkReferences
            {
                BuildEngine = new MockNeverCacheBuildEngine4(),
                EnableTargetingPackDownload = true,
                TargetFrameworkIdentifier = ".NETCoreApp",
                TargetFrameworkVersion = "5.0",
                TargetPlatformIdentifier = "Windows",
                TargetPlatformVersion = "10.0.18362",
                NETCoreSdkRuntimeIdentifier = "win-x64",
                RuntimeGraphPath =
                    runtimeGraphPathPath,
                TargetLatestRuntimePatch = true,
                TargetLatestRuntimePatchIsDefault = true,
                EnableRuntimePackDownload = true,
                FrameworkReferences =
                    new[]
                    {
                        new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>()),
                        new MockTaskItem("Microsoft.Windows.SDK.NET.Ref", new Dictionary<string, string>())
                    },
                KnownFrameworkReferences = new[]
                {
                    _netcoreAppKnownFrameworkReference, _validWindowsSDKKnownFrameworkReference,
                }
            };

            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                task.Execute().Should().BeFalse("IsWindowsOnly=true");
                return;
            }
            task.Execute().Should().BeTrue();

            task.TargetingPacks.Should().NotBeNull().And.HaveCount(2);
            task.TargetingPacks.Should().Contain(p =>
                p.GetMetadata(MetadataKeys.NuGetPackageId) == "Microsoft.Windows.SDK.NET.Ref");
            task.TargetingPacks.Should()
                .Contain(p => p.GetMetadata(MetadataKeys.NuGetPackageId) == "Microsoft.NETCore.App.Ref");

            task.RuntimePacks.Should().NotBeNull().And.HaveCount(1);
            task.RuntimePacks[0].ItemSpec.Should().Be("Microsoft.Windows.SDK.NET.Ref",
                "it should not resolve runtime pack for Microsoft.NETCore.App");
        }

        [Fact]
        public void It_processes_RuntimeIdentifiers_without_RuntimeIdentifier()
        {
            const string minimalRuntimeGraphPathContent =
                "{\"runtimes\":{\"any\":{\"#import\":[\"base\"]},\"base\":{\"#import\":[]},\"win\":{\"#import\":[\"any\"]},\"win-x64\":{\"#import\":[\"win\"]},\"win-x86\":{\"#import\":[\"win\"]},\"linux-x64\":{\"#import\":[\"any\"]}}}";
            var runtimeGraphPathPath = Path.GetTempFileName();
            File.WriteAllText(runtimeGraphPathPath, minimalRuntimeGraphPathContent);

            var task = new ProcessFrameworkReferences
            {
                BuildEngine = new MockNeverCacheBuildEngine4(),
                EnableTargetingPackDownload = true,
                TargetFrameworkIdentifier = ".NETCoreApp",
                TargetFrameworkVersion = "5.0",
                NETCoreSdkRuntimeIdentifier = "win-x64",
                RuntimeIdentifier = null, // No RuntimeIdentifier
                RuntimeIdentifiers = new[] { "win-x64", "win-x86", "linux-x64" }, // Multiple RuntimeIdentifiers
                RuntimeGraphPath = runtimeGraphPathPath,
                TargetLatestRuntimePatch = true,
                EnableRuntimePackDownload = true,
                FrameworkReferences = new[]
                {
                    new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>())
                },
                KnownFrameworkReferences = new[]
                {
                    new MockTaskItem("Microsoft.NETCore.App",
                        new Dictionary<string, string>
                        {
                            {"TargetFramework", "net5.0"},
                            {"RuntimeFrameworkName", "Microsoft.NETCore.App"},
                            {"DefaultRuntimeFrameworkVersion", "5.0.0"},
                            {"LatestRuntimeFrameworkVersion", "5.0.0"},
                            {"TargetingPackName", "Microsoft.NETCore.App.Ref"},
                            {"TargetingPackVersion", "5.0.0"},
                            {"RuntimePackNamePatterns", "Microsoft.NETCore.App.Runtime.**RID**"},
                            {"RuntimePackRuntimeIdentifiers", "win-x64;win-x86;linux-x64"},
                        })
                }
            };

            task.Execute().Should().BeTrue();

            // Should download targeting pack
            task.PackagesToDownload.Should().NotBeNull();
            task.PackagesToDownload.Should().Contain(p => p.ItemSpec == "Microsoft.NETCore.App.Ref");

            // Runtime packs for RuntimeIdentifiers should be downloaded even without a primary RuntimeIdentifier
            // This is the current behavior in ProcessFrameworkReferences.cs lines 376-400
            task.PackagesToDownload.Should().Contain(p => p.ItemSpec == "Microsoft.NETCore.App.Runtime.win-x64");
            task.PackagesToDownload.Should().Contain(p => p.ItemSpec == "Microsoft.NETCore.App.Runtime.win-x86");
            task.PackagesToDownload.Should().Contain(p => p.ItemSpec == "Microsoft.NETCore.App.Runtime.linux-x64");
        }

        [Fact]
        public void It_processes_RuntimeIdentifiers_with_empty_RuntimeIdentifier()
        {
            const string minimalRuntimeGraphPathContent =
                "{\"runtimes\":{\"any\":{\"#import\":[\"base\"]},\"base\":{\"#import\":[]},\"win\":{\"#import\":[\"any\"]},\"win-x64\":{\"#import\":[\"win\"]},\"linux-x64\":{\"#import\":[\"any\"]}}}";
            var runtimeGraphPathPath = Path.GetTempFileName();
            File.WriteAllText(runtimeGraphPathPath, minimalRuntimeGraphPathContent);

            var task = new ProcessFrameworkReferences
            {
                BuildEngine = new MockNeverCacheBuildEngine4(),
                EnableTargetingPackDownload = true,
                TargetFrameworkIdentifier = ".NETCoreApp",
                TargetFrameworkVersion = "5.0",
                NETCoreSdkRuntimeIdentifier = "win-x64",
                RuntimeIdentifier = "", // Empty RuntimeIdentifier
                RuntimeIdentifiers = new[] { "win-x64", "linux-x64" },
                RuntimeGraphPath = runtimeGraphPathPath,
                SelfContained = false,
                EnableRuntimePackDownload = true,
                FrameworkReferences = new[]
                {
                    new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>())
                },
                KnownFrameworkReferences = new[]
                {
                    _netcoreAppKnownFrameworkReference
                }
            };

            task.Execute().Should().BeTrue();

            // Should download targeting pack
            task.PackagesToDownload.Should().NotBeNull();
            task.PackagesToDownload.Should().Contain(p => p.ItemSpec == "Microsoft.NETCore.App.Ref");
        }

        [Fact]
        public void It_handles_RuntimeIdentifiers_with_any_rid()
        {
            const string minimalRuntimeGraphPathContent =
                "{\"runtimes\":{\"any\":{\"#import\":[\"base\"]},\"base\":{\"#import\":[]},\"win-x64\":{\"#import\":[\"any\"]}}}";
            var runtimeGraphPathPath = Path.GetTempFileName();
            File.WriteAllText(runtimeGraphPathPath, minimalRuntimeGraphPathContent);

            var task = new ProcessFrameworkReferences
            {
                BuildEngine = new MockNeverCacheBuildEngine4(),
                EnableTargetingPackDownload = true,
                TargetFrameworkIdentifier = ".NETCoreApp",
                TargetFrameworkVersion = "5.0",
                NETCoreSdkRuntimeIdentifier = "win-x64",
                RuntimeIdentifier = "any", // 'any' RID should be treated as null
                RuntimeIdentifiers = new[] { "win-x64" },
                RuntimeGraphPath = runtimeGraphPathPath,
                EnableRuntimePackDownload = true,
                FrameworkReferences = new[]
                {
                    new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>())
                },
                KnownFrameworkReferences = new[]
                {
                    _netcoreAppKnownFrameworkReference
                }
            };

            task.Execute().Should().BeTrue();

            // Should download targeting pack
            task.PackagesToDownload.Should().NotBeNull();
            task.PackagesToDownload.Should().Contain(p => p.ItemSpec == "Microsoft.NETCore.App.Ref");

            // With 'any' RID, EffectiveRuntimeIdentifier should be null
            // But runtime pack for win-x64 in RuntimeIdentifiers should still be downloaded
            // as per the implementation in ProcessFrameworkReferences.cs
            task.PackagesToDownload.Should().Contain(p => p.ItemSpec == "Microsoft.NETCore.App.Runtime.win-x64");
        }

        [Fact]
        public void It_processes_RuntimeIdentifiers_with_AlwaysCopyLocal_and_no_RuntimeIdentifier()
        {
            const string minimalRuntimeGraphPathContent =
                "{\"runtimes\":{\"any\":{\"#import\":[\"base\"]},\"base\":{\"#import\":[]},\"win\":{\"#import\":[\"any\"]},\"win-x64\":{\"#import\":[\"win\"]}}}";
            var runtimeGraphPathPath = Path.GetTempFileName();
            File.WriteAllText(runtimeGraphPathPath, minimalRuntimeGraphPathContent);

            var task = new ProcessFrameworkReferences
            {
                BuildEngine = new MockNeverCacheBuildEngine4(),
                EnableTargetingPackDownload = true,
                EnableRuntimePackDownload = true,
                TargetFrameworkIdentifier = ".NETCoreApp",
                TargetFrameworkVersion = "5.0",
                TargetPlatformIdentifier = "Windows",
                TargetPlatformVersion = "10.0.18362",
                NETCoreSdkRuntimeIdentifier = "win-x64",
                RuntimeIdentifier = null, // No RuntimeIdentifier
                RuntimeIdentifiers = new[] { "win-x64" },
                RuntimeGraphPath = runtimeGraphPathPath,
                FrameworkReferences = new[]
                {
                    new MockTaskItem("Microsoft.Windows.SDK.NET.Ref", new Dictionary<string, string>())
                },
                KnownFrameworkReferences = new[]
                {
                    _validWindowsSDKKnownFrameworkReference // This has RuntimePackAlwaysCopyLocal = true
                }
            };

            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                task.Execute().Should().BeFalse("IsWindowsOnly=true");
                return;
            }

            task.Execute().Should().BeTrue();

            // Should have targeting pack
            task.TargetingPacks.Should().NotBeNull().And.HaveCount(1);

            // Should have runtime pack even without RuntimeIdentifier due to AlwaysCopyLocal
            task.RuntimePacks.Should().NotBeNull().And.HaveCount(1);
            task.RuntimePacks[0].ItemSpec.Should().Be("Microsoft.Windows.SDK.NET.Ref");
        }

        [Fact]
        public void It_handles_null_RuntimeIdentifiers_array()
        {
            const string minimalRuntimeGraphPathContent =
                "{\"runtimes\":{\"any\":{\"#import\":[\"base\"]},\"base\":{\"#import\":[]}}}";
            var runtimeGraphPathPath = Path.GetTempFileName();
            File.WriteAllText(runtimeGraphPathPath, minimalRuntimeGraphPathContent);

            var task = new ProcessFrameworkReferences
            {
                BuildEngine = new MockNeverCacheBuildEngine4(),
                EnableTargetingPackDownload = true,
                TargetFrameworkIdentifier = ".NETCoreApp",
                TargetFrameworkVersion = "5.0",
                NETCoreSdkRuntimeIdentifier = "win-x64",
                RuntimeIdentifier = null,
                RuntimeIdentifiers = null, // Null RuntimeIdentifiers array
                RuntimeGraphPath = runtimeGraphPathPath,
                EnableRuntimePackDownload = true,
                FrameworkReferences = new[]
                {
                    new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>())
                },
                KnownFrameworkReferences = new[]
                {
                    _netcoreAppKnownFrameworkReference
                }
            };

            task.Execute().Should().BeTrue();

            // Should still download targeting pack
            task.PackagesToDownload.Should().NotBeNull();
            task.PackagesToDownload.Should().Contain(p => p.ItemSpec == "Microsoft.NETCore.App.Ref");
        }

        [Fact]
        public void It_processes_empty_RuntimeIdentifiers_array()
        {
            const string minimalRuntimeGraphPathContent =
                "{\"runtimes\":{\"any\":{\"#import\":[\"base\"]},\"base\":{\"#import\":[]}}}";
            var runtimeGraphPathPath = Path.GetTempFileName();
            File.WriteAllText(runtimeGraphPathPath, minimalRuntimeGraphPathContent);

            var task = new ProcessFrameworkReferences
            {
                BuildEngine = new MockNeverCacheBuildEngine4(),
                EnableTargetingPackDownload = true,
                TargetFrameworkIdentifier = ".NETCoreApp",
                TargetFrameworkVersion = "5.0",
                NETCoreSdkRuntimeIdentifier = "win-x64",
                RuntimeIdentifier = null,
                RuntimeIdentifiers = new string[] { }, // Empty RuntimeIdentifiers array
                RuntimeGraphPath = runtimeGraphPathPath,
                EnableRuntimePackDownload = true,
                FrameworkReferences = new[]
                {
                    new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>())
                },
                KnownFrameworkReferences = new[]
                {
                    _netcoreAppKnownFrameworkReference
                }
            };

            task.Execute().Should().BeTrue();

            // Should still download targeting pack
            task.PackagesToDownload.Should().NotBeNull();
            task.PackagesToDownload.Should().Contain(p => p.ItemSpec == "Microsoft.NETCore.App.Ref");
        }

        [Fact]
        public void It_handles_real_world_ridless_scenario_with_aot_and_trimming()
        {
            // This test reproduces the exact scenario from the reported issue
            // where the task would crash due to null RID handling issues
            const string runtimeGraphContent =
                "{\"runtimes\":{\"any\":{\"#import\":[\"base\"]},\"base\":{\"#import\":[]},\"osx\":{\"#import\":[\"any\"]},\"osx-arm64\":{\"#import\":[\"osx\"]},\"osx-x64\":{\"#import\":[\"osx\"]}}}";
            var runtimeGraphPathPath = Path.GetTempFileName();
            File.WriteAllText(runtimeGraphPathPath, runtimeGraphContent);

            var task = new ProcessFrameworkReferences
            {
                BuildEngine = new MockNeverCacheBuildEngine4(),
                TargetFrameworkIdentifier = ".NETCoreApp",
                TargetFrameworkVersion = "10.0",
                TargetPlatformIdentifier = "macOS",
                TargetPlatformVersion = "15.5",
                EnableTargetingPackDownload = true,
                EnableRuntimePackDownload = true,
                RequiresILLinkPack = true,
                PublishAot = true,
                PublishTrimmed = true,
                EnableAotAnalyzer = true,
                EnableTrimAnalyzer = true,
                EnableSingleFileAnalyzer = true,
                NETCoreSdkRuntimeIdentifier = "osx-arm64",
                NETCoreSdkPortableRuntimeIdentifier = "osx-arm64",
                RuntimeIdentifier = null, // No primary RuntimeIdentifier (key to reproducing the issue)
                RuntimeGraphPath = runtimeGraphPathPath,
                NetCoreRoot = "/usr/local/share/dotnet",
                NETCoreSdkVersion = "10.0.100-rc.2.25457.102",
                MinNonEolTargetFrameworkForAot = "net8.0",
                MinNonEolTargetFrameworkForTrimming = "net8.0",
                MinNonEolTargetFrameworkForSingleFile = "net8.0",
                FirstTargetFrameworkVersionToSupportAotAnalyzer = "7.0",
                FirstTargetFrameworkVersionToSupportTrimAnalyzer = "6.0",
                FirstTargetFrameworkVersionToSupportSingleFileAnalyzer = "6.0",
                AotUseKnownRuntimePackForTarget = true,
                DisableTransitiveFrameworkReferenceDownloads = true,
                FrameworkReferences = new[]
                {
                    new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>
                    {
                        ["IsImplicitlyDefined"] = "true",
                        ["PrivateAssets"] = "All",
                        ["Pack"] = "false"
                    }),
                    new MockTaskItem("Microsoft.macOS", new Dictionary<string, string>
                    {
                        ["IsImplicitlyDefined"] = "true",
                        ["PrivateAssets"] = "All",
                        ["Pack"] = "false"
                    })
                },
                KnownFrameworkReferences = new[]
                {
                    // Microsoft.NETCore.App for net10.0
                    new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>
                    {
                        ["TargetFramework"] = "net10.0",
                        ["RuntimeFrameworkName"] = "Microsoft.NETCore.App",
                        ["DefaultRuntimeFrameworkVersion"] = "10.0.0-rc.2.25457.102",
                        ["LatestRuntimeFrameworkVersion"] = "10.0.0-rc.2.25457.102",
                        ["TargetingPackName"] = "Microsoft.NETCore.App.Ref",
                        ["TargetingPackVersion"] = "10.0.0-rc.2.25457.102",
                        ["RuntimePackNamePatterns"] = "Microsoft.NETCore.App.Runtime.**RID**",
                        ["RuntimePackRuntimeIdentifiers"] = "linux-arm;linux-arm64;linux-musl-arm64;linux-musl-x64;linux-x64;osx-x64;tizen.4.0.0-armel;tizen.5.0.0-armel;win-arm64;win-x64;win-x86;linux-musl-arm;osx-arm64;linux-s390x;linux-loongarch64;linux-bionic-arm;linux-bionic-arm64;linux-bionic-x64;linux-bionic-x86;linux-ppc64le;freebsd-x64;freebsd-arm64;linux-riscv64;linux-musl-riscv64;linux-musl-loongarch64;android-arm64;android-x64"
                    }),
                    // Microsoft.macOS for net10.0
                    new MockTaskItem("Microsoft.macOS", new Dictionary<string, string>
                    {
                        ["TargetFramework"] = "net10.0",
                        ["RuntimeFrameworkName"] = "Microsoft.macOS",
                        ["DefaultRuntimeFrameworkVersion"] = "15.5.10834-ci.darc-net10-0-3e2d6574-3e2e-4233-aab9-99cf75de33e4",
                        ["LatestRuntimeFrameworkVersion"] = "15.5.10834-ci.darc-net10-0-3e2d6574-3e2e-4233-aab9-99cf75de33e4",
                        ["TargetingPackName"] = "Microsoft.macOS.Ref.net10.0_15.5",
                        ["TargetingPackVersion"] = "15.5.10834-ci.darc-net10-0-3e2d6574-3e2e-4233-aab9-99cf75de33e4",
                        ["RuntimePackNamePatterns"] = "Microsoft.macOS.Runtime.osx.net10.0_15.5",
                        ["RuntimePackRuntimeIdentifiers"] = "osx-x64;osx-arm64",
                        ["Profile"] = "macOS"
                    })
                },
                KnownRuntimePacks = new[]
                {
                    // NativeAOT runtime pack for net10.0
                    new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>
                    {
                        ["TargetFramework"] = "net10.0",
                        ["RuntimeFrameworkName"] = "Microsoft.NETCore.App",
                        ["LatestRuntimeFrameworkVersion"] = "10.0.0-rc.2.25457.102",
                        ["RuntimePackNamePatterns"] = "Microsoft.NETCore.App.Runtime.NativeAOT.**RID**",
                        ["RuntimePackRuntimeIdentifiers"] = "ios-arm64;iossimulator-arm64;iossimulator-x64;tvos-arm64;tvossimulator-arm64;tvossimulator-x64;maccatalyst-arm64;maccatalyst-x64;linux-bionic-arm64;linux-bionic-x64;osx-arm64;osx-x64;freebsd-arm64;freebsd-x64;linux-x64;linux-arm;linux-arm64;linux-loongarch64;linux-bionic-arm;linux-musl-x64;linux-musl-arm;linux-musl-arm64;linux-musl-loongarch64;win-x64;win-x86;win-arm64;browser-wasm;wasi-wasm;linux-riscv64;linux-musl-riscv64;android-arm64;android-x64",
                        ["RuntimePackLabels"] = "NativeAOT"
                    })
                },
                KnownILLinkPacks = new[]
                {
                    new MockTaskItem("Microsoft.NET.ILLink.Tasks", new Dictionary<string, string>
                    {
                        ["TargetFramework"] = "net10.0",
                        ["ILLinkPackVersion"] = "10.0.0-rc.2.25457.102"
                    })
                },
                KnownILCompilerPacks = new[]
                {
                    new MockTaskItem("Microsoft.DotNet.ILCompiler", new Dictionary<string, string>
                    {
                        ["TargetFramework"] = "net10.0",
                        ["ILCompilerPackNamePattern"] = "runtime.**RID**.Microsoft.DotNet.ILCompiler",
                        ["ILCompilerRuntimePackNamePattern"] = "Microsoft.NETCore.App.Runtime.NativeAOT.**RID**",
                        ["ILCompilerPackVersion"] = "10.0.0-rc.2.25457.102",
                        ["ILCompilerRuntimeIdentifiers"] = "linux-arm64;linux-musl-arm64;linux-musl-x64;linux-x64;win-arm64;win-x64;osx-x64;osx-arm64;freebsd-x64;freebsd-arm64;linux-arm;linux-musl-arm;linux-loongarch64;linux-musl-loongarch64;win-x86;linux-riscv64;linux-musl-riscv64"
                    })
                }
            };

            // This should not crash and should complete successfully
            // The original issue was that null RuntimeIdentifier caused crashes
            task.Execute().Should().BeTrue();

            // Should have targeting packs for both frameworks
            task.TargetingPacks.Should().NotBeNull();
            task.TargetingPacks.Should().Contain(p => p.GetMetadata(MetadataKeys.NuGetPackageId) == "Microsoft.NETCore.App.Ref");
            task.TargetingPacks.Should().Contain(p => p.GetMetadata(MetadataKeys.NuGetPackageId) == "Microsoft.macOS.Ref.net10.0_15.5");

            // Should download necessary packages including tool packs for AOT/trimming
            task.PackagesToDownload.Should().NotBeNull();
            task.ImplicitPackageReferences.Should().NotBeNull();

            // Key validation: The task should complete successfully without crashing
            // This was the main issue reported - the task would crash with null reference when
            // there was no RuntimeIdentifier but RuntimeIdentifiers were present

            // Should include the targeting packs
            task.PackagesToDownload.Should().Contain(p => p.ItemSpec == "Microsoft.NETCore.App.Ref");
            task.PackagesToDownload.Should().Contain(p => p.ItemSpec == "Microsoft.macOS.Ref.net10.0_15.5");

            // Should include ILCompiler pack for AOT (validates that AOT processing works)
            task.PackagesToDownload.Should().Contain(p => p.ItemSpec.StartsWith("runtime.") && p.ItemSpec.Contains("Microsoft.DotNet.ILCompiler"));
        }
    }
}
