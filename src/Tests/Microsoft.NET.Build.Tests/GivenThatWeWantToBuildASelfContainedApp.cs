﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Build.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildASelfContainedApp : SdkTest
    {
        public GivenThatWeWantToBuildASelfContainedApp(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("netcoreapp1.1", false)]
        [InlineData("netcoreapp2.0", false)]
        [InlineData("netcoreapp3.0", true)]
        public void It_builds_a_runnable_output(string targetFramework, bool dependenciesIncluded)
        {
            if (!EnvironmentInfo.SupportsTargetFramework(targetFramework))
            {
                return;
            }

            var runtimeIdentifier = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: targetFramework)
                .WithSource()
                .WithTargetFramework(targetFramework)
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement(ns + "RuntimeIdentifier", runtimeIdentifier));
                });

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework, runtimeIdentifier: runtimeIdentifier);
            var selfContainedExecutable = $"HelloWorld{Constants.ExeSuffix}";

            string selfContainedExecutableFullPath = Path.Combine(outputDirectory.FullName, selfContainedExecutable);

            string[] expectedFiles = new[] {
                selfContainedExecutable,
                "HelloWorld.dll",
                "HelloWorld.pdb",
                "HelloWorld.deps.json",
                "HelloWorld.runtimeconfig.dev.json",
                "HelloWorld.runtimeconfig.json",
                $"{FileConstants.DynamicLibPrefix}hostfxr{FileConstants.DynamicLibSuffix}",
                $"{FileConstants.DynamicLibPrefix}hostpolicy{FileConstants.DynamicLibSuffix}",
            };

            if (dependenciesIncluded)
            {
                outputDirectory.Should().HaveFiles(expectedFiles);
            }
            else
            {
                outputDirectory.Should().OnlyHaveFiles(expectedFiles);
            }

            outputDirectory.Should().NotHaveFiles(new[] {
                $"apphost{Constants.ExeSuffix}",
            });

            new RunExeCommand(Log, selfContainedExecutableFullPath)
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World!");
        }

        [Fact]
        public void It_errors_out_when_RuntimeIdentifier_architecture_and_PlatformTarget_do_not_match()
        {
            const string RuntimeIdentifier = $"{ToolsetInfo.LatestWinRuntimeIdentifier}-x64";
            const string PlatformTarget = "x86";

            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement(ns + "RuntimeIdentifier", RuntimeIdentifier));
                    propertyGroup.Add(new XElement(ns + "PlatformTarget", PlatformTarget));
                });

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Fail()
                .And.HaveStdOutContaining(string.Format(
                    Strings.CannotHaveRuntimeIdentifierPlatformMismatchPlatformTarget,
                    RuntimeIdentifier,
                    PlatformTarget));
        }

        [Fact]
        public void It_succeeds_when_RuntimeIdentifier_and_PlatformTarget_mismatch_but_PT_is_AnyCPU()
        {
            var targetFramework = ToolsetInfo.CurrentTargetFramework;
            var runtimeIdentifier = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement(ns + "RuntimeIdentifier", runtimeIdentifier));
                    propertyGroup.Add(new XElement(ns + "PlatformTarget", "AnyCPU"));
                });

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework, runtimeIdentifier: runtimeIdentifier);
            var selfContainedExecutable = $"HelloWorld{Constants.ExeSuffix}";

            string selfContainedExecutableFullPath = Path.Combine(outputDirectory.FullName, selfContainedExecutable);

            new RunExeCommand(Log, selfContainedExecutableFullPath)
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World!");
        }

        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void It_resolves_runtimepack_from_packs_folder()
        {
            var testProject = new TestProject()
            {
                IsExe = true,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid()
            };

            //  Use separate packages download folder for this project so that we can verify whether it had to download runtime packs
            testProject.AdditionalProperties["RestorePackagesPath"] = @"$(MSBuildProjectDirectory)\packages";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var getValuesCommand = new GetValuesCommand(testAsset, "RuntimePack", GetValuesCommand.ValueType.Item);
            getValuesCommand.MetadataNames = new List<string>() { "NuGetPackageId", "NuGetPackageVersion" };
            getValuesCommand.DependsOnTargets = "ProcessFrameworkReferences";
            getValuesCommand.ShouldRestore = false;

            getValuesCommand.Execute()
                .Should()
                .Pass();

            var runtimePacks = getValuesCommand.GetValuesWithMetadata();

            var packageDownloadProject = new TestProject()
            {
                Name = "PackageDownloadProject",
                TargetFrameworks = testProject.TargetFrameworks
            };

            //  Add PackageDownload items for runtime packs which will be needed
            foreach (var runtimePack in runtimePacks)
            {
                packageDownloadProject.AddItem("PackageDownload",
                    new Dictionary<string, string>()
                    {
                        {"Include", runtimePack.metadata["NuGetPackageId"] },
                        {"Version", "[" + runtimePack.metadata["NuGetPackageVersion"] + "]" }
                    });
            }

            //  Download runtime packs into separate folder under test assets
            packageDownloadProject.AdditionalProperties["RestorePackagesPath"] = @"$(MSBuildProjectDirectory)\packs";

            var packageDownloadAsset = _testAssetsManager.CreateTestProject(packageDownloadProject);

            new RestoreCommand(packageDownloadAsset)
                .Execute()
                .Should()
                .Pass();

            //  Package download folders use lowercased package names, but pack folders use mixed case
            //  So change casing of the downloaded runtime pack folders to match what is expected
            //  for packs folders
            foreach (var runtimePack in runtimePacks)
            {
                string oldCasing = Path.Combine(packageDownloadAsset.TestRoot, packageDownloadProject.Name, "packs", runtimePack.metadata["NuGetPackageId"].ToLowerInvariant());
                string newCasing = Path.Combine(packageDownloadAsset.TestRoot, packageDownloadProject.Name, "packs", runtimePack.metadata["NuGetPackageId"]);
                Directory.Move(oldCasing, newCasing);
            }

            //  Now build the original test project with the packs folder with the runtime packs we just downloaded
            var buildCommand = new BuildCommand(testAsset)
                .WithEnvironmentVariable(EnvironmentVariableNames.WORKLOAD_PACK_ROOTS, Path.Combine(packageDownloadAsset.TestRoot, packageDownloadProject.Name));

            buildCommand
                .Execute()
                .Should()
                .Pass();

            //  Verify that runtime packs weren't downloaded to test project's packages folder
            var packagesFolder = Path.Combine(testAsset.TestRoot, testProject.Name, "packages");
            foreach (var runtimePack in runtimePacks)
            {
                var path = Path.Combine(packagesFolder, runtimePack.metadata["NuGetPackageId"].ToLowerInvariant());
                new DirectoryInfo(path).Should().NotExist("Runtime Pack should have been resolved from packs folder");
            }
        }

        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void It_resolves_pack_versions_from_workload_manifest()
        {
            static string GetVersionBand(string sdkVersion)
            {
                if (!Version.TryParse(sdkVersion.Split('-')[0], out var sdkVersionParsed))
                {
                    throw new ArgumentException($"'{nameof(sdkVersion)}' should be a version, but get {sdkVersion}");
                }

                static int Last2DigitsTo0(int versionBuild)
                {
                    return (versionBuild / 100) * 100;
                }

                return $"{sdkVersionParsed.Major}.{sdkVersionParsed.Minor}.{Last2DigitsTo0(sdkVersionParsed.Build)}";
            }

            var testProject = new TestProject()
            {
                IsExe = true,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid(),
                SelfContained = "true"
            };

            //  Set up test FrameworkReference that will use workload manifest to resolve versions
            testProject.ProjectChanges.Add(project =>
            {
                var itemGroup = XElement.Parse($@"
  <ItemGroup>
    <KnownFrameworkReference Include='Microsoft.NETCore.App.Test'
                          TargetFramework='{ToolsetInfo.CurrentTargetFramework}'
                          RuntimeFrameworkName='Microsoft.NETCore.App.Test'
                          DefaultRuntimeFrameworkVersion='**FromWorkload**'
                          LatestRuntimeFrameworkVersion='**FromWorkload**'
                          TargetingPackName='Microsoft.NETCore.App.Test.Ref'
                          TargetingPackVersion='**FromWorkload**'
                          RuntimePackNamePatterns='Microsoft.NETCore.App.Test.RuntimePack'
                          RuntimePackRuntimeIdentifiers='any'
                              />

    <FrameworkReference Include='Microsoft.NETCore.App.Test'/>
  </ItemGroup>");
                project.Root.Add(itemGroup);
            });

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            //  Set up test workload manifest that will suply targeting and runtime pack versions
            string sdkVersionBand = GetVersionBand(TestContext.Current.ToolsetUnderTest.SdkVersion);
            string manifestRoot = Path.Combine(testAsset.TestRoot, "manifests");
            string manifestFolder = Path.Combine(manifestRoot, sdkVersionBand, "RuntimePackVersionTestWorkload");
            Directory.CreateDirectory(manifestFolder);
            string manifestPath = Path.Combine(manifestFolder, "WorkloadManifest.json");
            File.WriteAllText(manifestPath, @"
{
    ""version"": ""6.0.0-test"",
    ""workloads"": {
        ""testruntimepackworkload"": {
            ""packs"": [
                ""Microsoft.NETCore.App.Test.RuntimePack"",
                ""Microsoft.NETCore.App.Test.Ref""
            ]
        }
    },
    ""packs"": {
        ""Microsoft.NETCore.App.Test.RuntimePack"": {
            ""kind"": ""framework"",
            ""version"": ""1.0.42-abc""
        },
        ""Microsoft.NETCore.App.Test.Ref"": {
            ""kind"": ""framework"",
            ""version"": ""1.0.42-xyz""
        }
    }
}
");

            //  Verify correct targeting pack version is resolved
            var getValuesCommand = (GetValuesCommand)new GetValuesCommand(testAsset, "TargetingPack", GetValuesCommand.ValueType.Item)
                .WithEnvironmentVariable(EnvironmentVariableNames.WORKLOAD_MANIFEST_ROOTS, manifestRoot);
            getValuesCommand.MetadataNames = new List<string>() { "NuGetPackageId", "NuGetPackageVersion" };
            getValuesCommand.DependsOnTargets = "ProcessFrameworkReferences";
            getValuesCommand.ShouldRestore = false;

            getValuesCommand.Execute()
                .Should()
                .Pass();

            var targetingPacks = getValuesCommand.GetValuesWithMetadata();
            var testTargetingPack = targetingPacks.Single(p => p.value == "Microsoft.NETCore.App.Test");
            testTargetingPack.metadata["NuGetPackageId"].Should().Be("Microsoft.NETCore.App.Test.Ref");
            testTargetingPack.metadata["NuGetPackageVersion"].Should().Be("1.0.42-xyz");

            //  Verify correct runtime pack version is resolved
            getValuesCommand = (GetValuesCommand)new GetValuesCommand(testAsset, "RuntimePack", GetValuesCommand.ValueType.Item)
                .WithEnvironmentVariable(EnvironmentVariableNames.WORKLOAD_MANIFEST_ROOTS, manifestRoot);
            getValuesCommand.MetadataNames = new List<string>() { "NuGetPackageId", "NuGetPackageVersion" };
            getValuesCommand.DependsOnTargets = "ProcessFrameworkReferences";
            getValuesCommand.ShouldRestore = false;

            getValuesCommand.Execute()
                .Should()
                .Pass();

            var runtimePacks = getValuesCommand.GetValuesWithMetadata();
            var testRuntimePack = runtimePacks.Single(p => p.value == "Microsoft.NETCore.App.Test.RuntimePack");
            testRuntimePack.metadata["NuGetPackageId"].Should().Be("Microsoft.NETCore.App.Test.RuntimePack");
            testRuntimePack.metadata["NuGetPackageVersion"].Should().Be("1.0.42-abc");
        }

        [RequiresMSBuildVersionTheory("17.4.0.51802")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void It_can_publish_runtime_specific_apps_with_library_dependencies_self_contained(string targetFramework)
        {

            // There's a bug when using the 6.0 SDK with 17.4 but we have limited control over the VS version used in helix
            Version.TryParse(TestContext.Current.ToolsetUnderTest.MSBuildVersion, out Version msbuildVersion);
            Version.TryParse("17.4.0", out Version maximumVersion);
            if (msbuildVersion >= maximumVersion)
                return;

            // create a basic library and a basic app, reference the library from the app and then
            // publish the app with a RID specified and self-contained.
            // verify that no warnings about missing the --self-contained flag are emitted.
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var libProject = new TestProject("RidSelfContainedLib")
            {
                IsExe = false,
                TargetFrameworks = targetFramework,
                IsSdkProject = true
            };
            var createdLibProject = _testAssetsManager.CreateTestProject(libProject);
            var appProject = new TestProject("RidSelfContainedApp")
            {
                IsExe = true,
                TargetFrameworks = targetFramework,
                IsSdkProject = true
            };
            appProject.ReferencedProjects.Add(libProject);
            var createdAppProject = _testAssetsManager.CreateTestProject(appProject);
            var publishCommand = new PublishCommand(createdAppProject);
            publishCommand.Execute(new[] { "-property:SelfContained=true", "-property:_CommandLineDefinedSelfContained=true", $"-property:RuntimeIdentifier={rid}", "-property:_CommandLineDefinedRuntimeIdentifier=true" }).Should().Pass().And.NotHaveStdOutContaining("warning");
        }

        [Theory]
        [InlineData("net7.0")]
        [InlineData("net8.0")]
        public void It_does_or_doesnt_imply_SelfContained_based_on_RuntimeIdentifier_and_TargetFramework(string targetFramework)
        {
            var runtimeIdentifier = EnvironmentInfo.GetCompatibleRid(targetFramework);
            bool resultShouldBeSelfContained = targetFramework == "net7.0" ? true : false;
            var testProject = new TestProject("MainProject")
            {
                TargetFrameworks = targetFramework,
                IsExe = true
            };

            testProject.RecordProperties("SelfContained");
            testProject.AdditionalProperties["RuntimeIdentifier"] = runtimeIdentifier;

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);
            new DotnetBuildCommand(Log)
                .WithWorkingDirectory(Path.Combine(testAsset.Path, "MainProject"))
                .Execute()
                .Should()
                .Pass();

            var properties = testProject.GetPropertyValues(testAsset.TestRoot, targetFramework: targetFramework);
            Assert.Equal(bool.Parse(properties["SelfContained"]), resultShouldBeSelfContained);
        }

        [Theory]
        [InlineData("net7.0", true)]
        [InlineData("net7.0", false)]
        [InlineData("net8.0", false)]
        public void It_does_or_doesnt_warn_based_on_SelfContained_and_TargetFramework_breaking_RID_change(string targetFramework, bool defineSelfContained)
        {
            var runtimeIdentifier = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: targetFramework + defineSelfContained.ToString())
                .WithSource()
                .WithTargetFramework(targetFramework)
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement(ns + "RuntimeIdentifier", runtimeIdentifier));
                    if (defineSelfContained)
                    {
                        propertyGroup.Add(new XElement(ns + "SelfContained", "true"));
                    }
                });

            var buildCommand = new DotnetBuildCommand(Log, "/bl");
            var commandResult = buildCommand
                .WithWorkingDirectory(testAsset.Path)
                .Execute();

            if (targetFramework == "net7.0" && !defineSelfContained)
            {
                commandResult
                    .Should()
                    .Pass()
                    .And
                    .HaveStdOutContaining(Strings.RuntimeIdentifierWillNoLongerImplySelfContained);
            }
            else
            {
                commandResult
                    .Should()
                    .Pass()
                    .And
                    .NotHaveStdOutContaining(Strings.RuntimeIdentifierWillNoLongerImplySelfContained);
            }
        }

        [Fact]
        public void It_does_not_build_SelfContained_due_to_PublishSelfContained_being_true()
        {
            string targetFramework = ToolsetInfo.CurrentTargetFramework;

            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: "ItDoesNotBuildSCDueToPSC")
                .WithSource()
                .WithTargetFramework(targetFramework)
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement(ns + "PublishSelfContained", "true"));
                });

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework);
            outputDirectory.Should().NotHaveFile("hostfxr.dll"); // This file will only appear if SelfContained. 
        }

        [Theory]
        [InlineData("PublishReadyToRun")]
        [InlineData("PublishSingleFile")]
        [InlineData("PublishSelfContained")]
        [InlineData("PublishAot")]
        public void It_builds_without_implicit_rid_with_RuntimeIdentifier_specific_during_publish_only_properties(string property)
        {
            var tfm = ToolsetInfo.CurrentTargetFramework;
            var testProject = new TestProject()
            {
                IsExe = true,
                TargetFrameworks = tfm,
            };
            testProject.AdditionalProperties[property] = "true";
            testProject.RecordProperties("RuntimeIdentifier");
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: property);

            var buildCommand = new DotnetBuildCommand(testAsset);
            buildCommand
               .Execute()
               .Should()
               .Pass();

            var properties = testProject.GetPropertyValues(testAsset.TestRoot, targetFramework: tfm);
            properties["RuntimeIdentifier"].Should().Be("");
        }

        [Theory]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void It_builds_a_runnable_output_with_Prefer32Bit(string targetFramework)
        {
            if (!EnvironmentInfo.SupportsTargetFramework(targetFramework))
            {
                return;
            }

            var runtimeIdentifier = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: targetFramework)
                .WithSource()
                .WithTargetFramework(targetFramework)
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement(ns + "RuntimeIdentifier", runtimeIdentifier));
                    propertyGroup.Add(new XElement(ns + "Prefer32Bit", "true"));
                });

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining(Strings.Prefer32BitIgnoredForNetCoreApp);

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework, runtimeIdentifier: runtimeIdentifier);
            var selfContainedExecutable = $"HelloWorld{Constants.ExeSuffix}";

            string selfContainedExecutableFullPath = Path.Combine(outputDirectory.FullName, selfContainedExecutable);
            new RunExeCommand(Log, selfContainedExecutableFullPath)
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World!");
        }
    }
}
