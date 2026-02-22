// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Reflection;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    /// <summary>
    /// Behavioral tests for attribute-only tasks in merge-group-5.
    /// These tasks received only the [MSBuildMultiThreadableTask] attribute — no source
    /// code changes — so we verify they still produce correct results.
    /// </summary>
    public class GivenAttributeOnlyTasksGroup5
    {
        #region Attribute Presence

        [Fact]
        public void CollectSDKReferencesDesignTime_HasMultiThreadableAttribute()
        {
            typeof(CollectSDKReferencesDesignTime).GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>()
                .Should().NotBeNull("task must be decorated with [MSBuildMultiThreadableTask]");
        }

        [Fact]
        public void CreateWindowsSdkKnownFrameworkReferences_HasMultiThreadableAttribute()
        {
            typeof(CreateWindowsSdkKnownFrameworkReferences).GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>()
                .Should().NotBeNull("task must be decorated with [MSBuildMultiThreadableTask]");
        }

        [Fact]
        public void FindItemsFromPackages_HasMultiThreadableAttribute()
        {
            typeof(FindItemsFromPackages).GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>()
                .Should().NotBeNull("task must be decorated with [MSBuildMultiThreadableTask]");
        }

        [Fact]
        public void GetAssemblyVersion_HasMultiThreadableAttribute()
        {
            typeof(GetAssemblyVersion).GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>()
                .Should().NotBeNull("task must be decorated with [MSBuildMultiThreadableTask]");
        }

        [Fact]
        public void GenerateSupportedTargetFrameworkAlias_HasMultiThreadableAttribute()
        {
            typeof(GenerateSupportedTargetFrameworkAlias).GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>()
                .Should().NotBeNull("task must be decorated with [MSBuildMultiThreadableTask]");
        }

        #endregion

        #region CollectSDKReferencesDesignTime

        [Fact]
        public void CollectSDKReferencesDesignTime_CollectsSdkReferencesAndImplicitPackages()
        {
            var sdkRef = new TaskItem("Microsoft.NETCore.App");
            sdkRef.SetMetadata("SDKPackageItemSpec", "");

            var implicitPkg = new MockTaskItem("Microsoft.NETCore.App", new Dictionary<string, string>
            {
                { "IsImplicitlyDefined", "true" },
                { "Version", "8.0.0" }
            });

            var explicitPkg = new MockTaskItem("Newtonsoft.Json", new Dictionary<string, string>
            {
                { "Version", "13.0.1" }
            });

            var task = new CollectSDKReferencesDesignTime
            {
                BuildEngine = new MockBuildEngine(),
                SdkReferences = new ITaskItem[] { sdkRef },
                PackageReferences = new ITaskItem[] { implicitPkg, explicitPkg },
                DefaultImplicitPackages = "Microsoft.NETCore.App"
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.SDKReferencesDesignTime.Should().NotBeNull();
            // Should include the SDK reference + the implicit package, but not the explicit package
            task.SDKReferencesDesignTime.Should().HaveCount(2);
            task.SDKReferencesDesignTime.Should().Contain(i => i.ItemSpec == "Microsoft.NETCore.App");
        }

        [Fact]
        public void CollectSDKReferencesDesignTime_ExcludesNonImplicitPackages()
        {
            var explicitPkg = new MockTaskItem("Newtonsoft.Json", new Dictionary<string, string>
            {
                { "Version", "13.0.1" }
            });

            var task = new CollectSDKReferencesDesignTime
            {
                BuildEngine = new MockBuildEngine(),
                SdkReferences = Array.Empty<ITaskItem>(),
                PackageReferences = new ITaskItem[] { explicitPkg },
                DefaultImplicitPackages = "Microsoft.NETCore.App"
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.SDKReferencesDesignTime.Should().BeEmpty(
                "explicit packages not in DefaultImplicitPackages should not be collected");
        }

        [Fact]
        public void CollectSDKReferencesDesignTime_HandlesEmptyDefaultImplicitPackages()
        {
            var task = new CollectSDKReferencesDesignTime
            {
                BuildEngine = new MockBuildEngine(),
                SdkReferences = Array.Empty<ITaskItem>(),
                PackageReferences = Array.Empty<ITaskItem>(),
                DefaultImplicitPackages = ""
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.SDKReferencesDesignTime.Should().BeEmpty();
        }

        #endregion

        #region CreateWindowsSdkKnownFrameworkReferences

        [Fact]
        public void CreateWindowsSdkKnownFrameworkReferences_WithExplicitPackageVersion_ProducesFiveProfiles()
        {
            var task = new CreateWindowsSdkKnownFrameworkReferences
            {
                BuildEngine = new MockBuildEngine(),
                WindowsSdkPackageVersion = "10.0.19041.31",
                TargetFrameworkIdentifier = ".NETCoreApp",
                TargetFrameworkVersion = "8.0",
                TargetPlatformIdentifier = "Windows",
                TargetPlatformVersion = "10.0.19041.0"
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.KnownFrameworkReferences.Should().HaveCount(5,
                "should produce 5 profile variants: default, Windows, Xaml, CsWinRT3.Windows, CsWinRT3.Xaml");

            // Verify the default (no profile) item
            var defaultItem = task.KnownFrameworkReferences.First(i => i.ItemSpec == "Microsoft.Windows.SDK.NET.Ref");
            defaultItem.GetMetadata("TargetingPackVersion").Should().Be("10.0.19041.31");
            defaultItem.GetMetadata("RuntimePackRuntimeIdentifiers").Should().Be("any");
        }

        [Fact]
        public void CreateWindowsSdkKnownFrameworkReferences_WithPreviewMode_AppendsPreviewSuffix()
        {
            var task = new CreateWindowsSdkKnownFrameworkReferences
            {
                BuildEngine = new MockBuildEngine(),
                UseWindowsSDKPreview = true,
                TargetFrameworkIdentifier = ".NETCoreApp",
                TargetFrameworkVersion = "9.0",
                TargetPlatformIdentifier = "Windows",
                TargetPlatformVersion = "10.0.26100.0"
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.KnownFrameworkReferences.Should().NotBeEmpty();
            task.KnownFrameworkReferences[0].GetMetadata("TargetingPackVersion")
                .Should().EndWith("-preview");
        }

        [Fact]
        public void CreateWindowsSdkKnownFrameworkReferences_ErrorsWhenBothVersionAndMinimumRevisionSet()
        {
            var engine = new MockBuildEngine();
            var task = new CreateWindowsSdkKnownFrameworkReferences
            {
                BuildEngine = engine,
                WindowsSdkPackageVersion = "10.0.19041.31",
                WindowsSdkPackageMinimumRevision = "32",
                TargetFrameworkVersion = "8.0",
                TargetPlatformVersion = "10.0.19041.0"
            };

            var result = task.Execute();

            result.Should().BeFalse("cannot specify both PackageVersion and MinimumRevision");
            engine.Errors.Should().NotBeEmpty();
        }

        #endregion

        #region FindItemsFromPackages

        [Fact]
        public void FindItemsFromPackages_ReturnsMatchingItems()
        {
            var item1 = new MockTaskItem("lib/net8.0/MyLib.dll", new Dictionary<string, string>
            {
                { "NuGetPackageId", "MyPackage" },
                { "NuGetPackageVersion", "1.0.0" }
            });
            var item2 = new MockTaskItem("lib/net8.0/Other.dll", new Dictionary<string, string>
            {
                { "NuGetPackageId", "OtherPackage" },
                { "NuGetPackageVersion", "2.0.0" }
            });

            var package = new MockTaskItem("MyPackage", new Dictionary<string, string>
            {
                { "NuGetPackageId", "MyPackage" },
                { "NuGetPackageVersion", "1.0.0" }
            });

            var task = new FindItemsFromPackages
            {
                BuildEngine = new MockBuildEngine(),
                Items = new ITaskItem[] { item1, item2 },
                Packages = new ITaskItem[] { package }
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.ItemsFromPackages.Should().HaveCount(1);
            task.ItemsFromPackages[0].ItemSpec.Should().Be("lib/net8.0/MyLib.dll");
        }

        [Fact]
        public void FindItemsFromPackages_ReturnsEmptyWhenNoMatch()
        {
            var item = new MockTaskItem("lib/net8.0/MyLib.dll", new Dictionary<string, string>
            {
                { "NuGetPackageId", "MyPackage" },
                { "NuGetPackageVersion", "1.0.0" }
            });

            var package = new MockTaskItem("OtherPackage", new Dictionary<string, string>
            {
                { "NuGetPackageId", "OtherPackage" },
                { "NuGetPackageVersion", "3.0.0" }
            });

            var task = new FindItemsFromPackages
            {
                BuildEngine = new MockBuildEngine(),
                Items = new ITaskItem[] { item },
                Packages = new ITaskItem[] { package }
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.ItemsFromPackages.Should().BeEmpty();
        }

        [Fact]
        public void FindItemsFromPackages_SkipsItemsWithoutPackageMetadata()
        {
            var itemWithMeta = new MockTaskItem("lib/net8.0/MyLib.dll", new Dictionary<string, string>
            {
                { "NuGetPackageId", "MyPackage" },
                { "NuGetPackageVersion", "1.0.0" }
            });
            var itemWithoutMeta = new MockTaskItem("lib/net8.0/NoMeta.dll", new Dictionary<string, string>());

            var package = new MockTaskItem("MyPackage", new Dictionary<string, string>
            {
                { "NuGetPackageId", "MyPackage" },
                { "NuGetPackageVersion", "1.0.0" }
            });

            var task = new FindItemsFromPackages
            {
                BuildEngine = new MockBuildEngine(),
                Items = new ITaskItem[] { itemWithMeta, itemWithoutMeta },
                Packages = new ITaskItem[] { package }
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.ItemsFromPackages.Should().HaveCount(1,
                "only items with matching NuGet metadata should be returned");
        }

        #endregion

        #region GetAssemblyVersion

        [Fact]
        public void GetAssemblyVersion_ParsesStandardVersion()
        {
            var task = new GetAssemblyVersion
            {
                BuildEngine = new MockBuildEngine(),
                NuGetVersion = "8.0.1"
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.AssemblyVersion.Should().Be("8.0.1.0");
        }

        [Fact]
        public void GetAssemblyVersion_ParsesPreReleaseVersion()
        {
            var task = new GetAssemblyVersion
            {
                BuildEngine = new MockBuildEngine(),
                NuGetVersion = "9.0.0-preview.1.24080.9"
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.AssemblyVersion.Should().Be("9.0.0.0");
        }

        [Fact]
        public void GetAssemblyVersion_LogsErrorForInvalidVersion()
        {
            var engine = new MockBuildEngine();
            var task = new GetAssemblyVersion
            {
                BuildEngine = engine,
                NuGetVersion = "not-a-version"
            };

            var result = task.Execute();

            result.Should().BeFalse();
            engine.Errors.Should().NotBeEmpty();
        }

        [Fact]
        public void GetAssemblyVersion_ParsesFourPartVersion()
        {
            var task = new GetAssemblyVersion
            {
                BuildEngine = new MockBuildEngine(),
                NuGetVersion = "1.2.3.4"
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.AssemblyVersion.Should().Be("1.2.3.4");
        }

        #endregion

        #region GenerateSupportedTargetFrameworkAlias

        [Fact]
        public void GenerateSupportedTargetFrameworkAlias_GeneratesAliasForMatchingFramework()
        {
            var tfm = new TaskItem(".NETCoreApp,Version=v8.0");
            tfm.SetMetadata("DisplayName", ".NET 8.0");

            var task = new GenerateSupportedTargetFrameworkAlias
            {
                BuildEngine = new MockBuildEngine(),
                SupportedTargetFramework = new ITaskItem[] { tfm },
                TargetFrameworkMoniker = ".NETCoreApp,Version=v8.0"
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.SupportedTargetFrameworkAlias.Should().HaveCount(1);
            task.SupportedTargetFrameworkAlias[0].ItemSpec.Should().Be("net8.0");
            task.SupportedTargetFrameworkAlias[0].GetMetadata("DisplayName").Should().Be(".NET 8.0");
        }

        [Fact]
        public void GenerateSupportedTargetFrameworkAlias_FiltersNonMatchingFrameworks()
        {
            var tfmNet8 = new TaskItem(".NETCoreApp,Version=v8.0");
            var tfmNetFx = new TaskItem(".NETFramework,Version=v4.7.2");

            var task = new GenerateSupportedTargetFrameworkAlias
            {
                BuildEngine = new MockBuildEngine(),
                SupportedTargetFramework = new ITaskItem[] { tfmNet8, tfmNetFx },
                TargetFrameworkMoniker = ".NETCoreApp,Version=v8.0"
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.SupportedTargetFrameworkAlias.Should().HaveCount(1,
                "only TFMs matching the target framework identifier should be included");
            task.SupportedTargetFrameworkAlias[0].ItemSpec.Should().Be("net8.0");
        }

        [Fact]
        public void GenerateSupportedTargetFrameworkAlias_AddsWindowsSuffixForWpf()
        {
            var tfm = new TaskItem(".NETCoreApp,Version=v8.0");

            var task = new GenerateSupportedTargetFrameworkAlias
            {
                BuildEngine = new MockBuildEngine(),
                SupportedTargetFramework = new ITaskItem[] { tfm },
                TargetFrameworkMoniker = ".NETCoreApp,Version=v8.0",
                UseWpf = true
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.SupportedTargetFrameworkAlias.Should().HaveCount(1);
            task.SupportedTargetFrameworkAlias[0].ItemSpec.Should().Contain("-windows",
                "WPF projects on .NET 5+ should get a -windows suffix");
        }

        [Fact]
        public void GenerateSupportedTargetFrameworkAlias_AddsWindowsSuffixForWindowsForms()
        {
            var tfm = new TaskItem(".NETCoreApp,Version=v8.0");

            var task = new GenerateSupportedTargetFrameworkAlias
            {
                BuildEngine = new MockBuildEngine(),
                SupportedTargetFramework = new ITaskItem[] { tfm },
                TargetFrameworkMoniker = ".NETCoreApp,Version=v8.0",
                UseWindowsForms = true
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.SupportedTargetFrameworkAlias.Should().HaveCount(1);
            task.SupportedTargetFrameworkAlias[0].ItemSpec.Should().Contain("-windows",
                "Windows Forms projects on .NET 5+ should get a -windows suffix");
        }

        [Fact]
        public void GenerateSupportedTargetFrameworkAlias_NoWindowsSuffixForOlderFrameworks()
        {
            // .NET Core 3.1 should NOT get -windows suffix even with UseWpf
            var tfm = new TaskItem(".NETCoreApp,Version=v3.1");

            var task = new GenerateSupportedTargetFrameworkAlias
            {
                BuildEngine = new MockBuildEngine(),
                SupportedTargetFramework = new ITaskItem[] { tfm },
                TargetFrameworkMoniker = ".NETCoreApp,Version=v3.1",
                UseWpf = true
            };

            var result = task.Execute();

            result.Should().BeTrue();
            task.SupportedTargetFrameworkAlias.Should().HaveCount(1);
            task.SupportedTargetFrameworkAlias[0].ItemSpec.Should().Be("netcoreapp3.1");
        }

        #endregion
    }
}
