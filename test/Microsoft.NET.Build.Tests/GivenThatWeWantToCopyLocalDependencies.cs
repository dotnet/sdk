// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.Build.Tests
{
    [TestClass]
    public class GivenThatWeWantToCopyLocalDependencies : SdkTest
    {

        [TestMethod]
        public void It_copies_local_package_dependencies_on_build()
        {
            const string ProjectName = "TestProjWithPackageDependencies";

            TestProject testProject = new()
            {
                Name = ProjectName,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()));
            testProject.PackageReferences.Add(new TestPackageReference("sqlite", "3.13.0"));

            var testProjectInstance = TestAssetsManager
               .CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testProjectInstance);

            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);

            var expectedFiles = new[]
            {
                $"{ProjectName}{Constants.ExeSuffix}",
                $"{ProjectName}.deps.json",
                $"{ProjectName}.dll",
                $"{ProjectName}.pdb",
                $"{ProjectName}.runtimeconfig.json",
                $"{ProjectName}.runtimeconfig.dev.json",
                "Newtonsoft.Json.dll",
                "runtimes/linux-x64/native/libsqlite3.so",
                "runtimes/osx-x64/native/libsqlite3.dylib",
                "runtimes/win7-x64/native/sqlite3.dll",
                "runtimes/win7-x86/native/sqlite3.dll"
            };

            outputDirectory.Should().OnlyHaveFiles(expectedFiles);
        }

        [TestMethod]
        public void It_does_not_copy_local_package_dependencies_when_requested_not_to()
        {
            const string ProjectName = "TestProjWithPackageDependencies";

            TestProject testProject = new()
            {
                Name = ProjectName,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.AdditionalProperties["CopyLocalLockFileAssemblies"] = "false";
            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()));
            testProject.PackageReferences.Add(new TestPackageReference("sqlite", "3.13.0"));

            var testProjectInstance = TestAssetsManager
               .CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testProjectInstance);

            buildCommand.Execute().Should().Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);
            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{ProjectName}{Constants.ExeSuffix}",
                $"{ProjectName}.deps.json",
                $"{ProjectName}.dll",
                $"{ProjectName}.pdb",
                $"{ProjectName}.runtimeconfig.json",
                $"{ProjectName}.runtimeconfig.dev.json",
            });
        }

        [TestMethod]
        public void It_copies_local_specific_runtime_package_dependencies_on_build()
        {
            const string ProjectName = "TestProjWithPackageDependencies";

            var rid = EnvironmentInfo.GetCompatibleRid(ToolsetInfo.CurrentTargetFramework);

            TestProject testProject = new()
            {
                Name = ProjectName,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.AdditionalProperties.Add("RuntimeIdentifier", rid);
            testProject.AdditionalProperties.Add("SelfContained", "false");
            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()));
            testProject.PackageReferences.Add(new TestPackageReference("Libuv", "1.10.0"));

            var testProjectInstance = TestAssetsManager
               .CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testProjectInstance);

            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework: testProject.TargetFrameworks, runtimeIdentifier: rid);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{ProjectName}{Constants.ExeSuffix}",
                $"{ProjectName}.deps.json",
                $"{ProjectName}.dll",
                $"{ProjectName}.pdb",
                $"{ProjectName}.runtimeconfig.json",
                $"{ProjectName}.runtimeconfig.dev.json",
                "Newtonsoft.Json.dll",
                // NOTE: this may break in the future when the SDK supports platforms that libuv does not
                $"libuv{FileConstants.DynamicLibSuffix}"
            });
        }

        [TestMethod]
        public void It_does_not_copy_local_package_dependencies_for_lib_projects()
        {
            const string ProjectName = "TestProjWithPackageDependencies";

            TestProject testProject = new()
            {
                Name = ProjectName,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = false
            };

            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()));
            testProject.PackageReferences.Add(new TestPackageReference("sqlite", "3.13.0"));

            var testProjectInstance = TestAssetsManager
               .CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testProjectInstance);

            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);
            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{ProjectName}.deps.json",
                $"{ProjectName}.dll",
                $"{ProjectName}.pdb",
            });
        }

        [TestMethod]
        public void It_copies_local_package_dependencies_for_lib_projects_when_requested_to()
        {
            const string ProjectName = "TestProjWithPackageDependencies";

            TestProject testProject = new()
            {
                Name = ProjectName,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = false
            };

            testProject.AdditionalProperties["CopyLocalLockFileAssemblies"] = "true";
            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()));
            testProject.PackageReferences.Add(new TestPackageReference("sqlite", "3.13.0"));

            var testProjectInstance = TestAssetsManager
               .CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testProjectInstance);

            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);
            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{ProjectName}.deps.json",
                $"{ProjectName}.dll",
                $"{ProjectName}.pdb",
                "Newtonsoft.Json.dll",
                "runtimes/linux-x64/native/libsqlite3.so",
                "runtimes/osx-x64/native/libsqlite3.dylib",
                "runtimes/win7-x64/native/sqlite3.dll",
                "runtimes/win7-x86/native/sqlite3.dll"
            });
        }

        [TestMethod]
        public void It_does_not_copy_local_package_dependencies_for_netstandard_projects()
        {
            const string ProjectName = "TestProjWithPackageDependencies";

            TestProject testProject = new()
            {
                Name = ProjectName,
                TargetFrameworks = "netstandard2.0"
            };

            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()));
            testProject.PackageReferences.Add(new TestPackageReference("sqlite", "3.13.0"));

            var testProjectInstance = TestAssetsManager
               .CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testProjectInstance);

            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);
            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{ProjectName}.deps.json",
                $"{ProjectName}.dll",
                $"{ProjectName}.pdb"
            });
        }

        [TestMethod]
        public void It_copies_local_package_dependencies_for_netstandard_projects_when_requested_to()
        {
            const string ProjectName = "TestProjWithPackageDependencies";

            TestProject testProject = new()
            {
                Name = ProjectName,
                TargetFrameworks = "netstandard2.0"
            };

            testProject.AdditionalProperties["CopyLocalLockFileAssemblies"] = "true";
            testProject.AdditionalProperties["CopyLocalRuntimeTargetAssets"] = "true";
            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()));
            testProject.PackageReferences.Add(new TestPackageReference("sqlite", "3.13.0"));

            var testProjectInstance = TestAssetsManager
               .CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testProjectInstance);

            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);
            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{ProjectName}.deps.json",
                $"{ProjectName}.dll",
                $"{ProjectName}.pdb",
                "Newtonsoft.Json.dll",
                "runtimes/linux-x64/native/libsqlite3.so",
                "runtimes/osx-x64/native/libsqlite3.dylib",
                "runtimes/win7-x64/native/sqlite3.dll",
                "runtimes/win7-x86/native/sqlite3.dll"
            });
        }

        [TestMethod]
        public void It_does_not_copy_local_runtime_dependencies_for_netframework_projects()
        {
            const string ProjectName = "TestProjWithPackageDependencies";

            TestProject testProject = new()
            {
                Name = ProjectName,
                TargetFrameworks = "net46"
            };

            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()));
            testProject.PackageReferences.Add(new TestPackageReference("sqlite", "3.13.0"));

            var testProjectInstance = TestAssetsManager
               .CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testProjectInstance);

            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);
            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{ProjectName}.dll",
                $"{ProjectName}.pdb",
                "Newtonsoft.Json.dll",
            });
        }

        [TestMethod]
        public void It_copies_local_all_assets_on_self_contained_build()
        {
            const string ProjectName = "TestProjWithPackageDependencies";

            var rid = EnvironmentInfo.GetCompatibleRid(ToolsetInfo.CurrentTargetFramework);

            TestProject testProject = new()
            {
                Name = ProjectName,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
                SelfContained = "true"
            };

            testProject.AdditionalProperties.Add("RuntimeIdentifier", rid);
            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()));
            testProject.PackageReferences.Add(new TestPackageReference("Libuv", "1.10.0"));

            var testProjectInstance = TestAssetsManager
               .CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testProjectInstance);

            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework: testProject.TargetFrameworks, runtimeIdentifier: rid);

            outputDirectory.Should().HaveFiles(new[] {
                $"{ProjectName}{Constants.ExeSuffix}",
                $"{ProjectName}.deps.json",
                $"{ProjectName}.dll",
                $"{ProjectName}.pdb",
                $"{ProjectName}.runtimeconfig.json",
                "Newtonsoft.Json.dll",
                // NOTE: this may break in the future when the SDK supports platforms that libuv does not
                $"libuv{FileConstants.DynamicLibSuffix}",
                $"{FileConstants.DynamicLibPrefix}clrjit{FileConstants.DynamicLibSuffix}",
                $"{FileConstants.DynamicLibPrefix}hostfxr{FileConstants.DynamicLibSuffix}",
                $"{FileConstants.DynamicLibPrefix}hostpolicy{FileConstants.DynamicLibSuffix}",
                $"mscorlib.dll",
                // This is not an exhaustive list as there are many files in self-contained builds
            });

            outputDirectory.Should().NotHaveFiles(new[] {
                $"apphost{Constants.ExeSuffix}",
            });
        }

        [TestMethod]
        public void It_filters_runtime_assets_by_BundledRuntimeAssetRuntimeIdentifiers()
        {
            const string ProjectName = "TestProjWithPackageDependencies";

            TestProject testProject = new()
            {
                Name = ProjectName,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            // sqlite package has RID-specific native assets for linux-x64, osx-x64, win7-x64, win7-x86.
            // Setting BundledRuntimeAssetRuntimeIdentifiers to linux-x64 should filter output to only linux-x64 assets.
            testProject.AdditionalProperties["BundledRuntimeAssetRuntimeIdentifiers"] = "linux-x64";
            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()));
            testProject.PackageReferences.Add(new TestPackageReference("sqlite", "3.13.0"));

            var testProjectInstance = TestAssetsManager
               .CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testProjectInstance);

            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);

            var expectedFiles = new[]
            {
                $"{ProjectName}{Constants.ExeSuffix}",
                $"{ProjectName}.deps.json",
                $"{ProjectName}.dll",
                $"{ProjectName}.pdb",
                $"{ProjectName}.runtimeconfig.json",
                $"{ProjectName}.runtimeconfig.dev.json",
                "Newtonsoft.Json.dll",
                "runtimes/linux-x64/native/libsqlite3.so",
            };

            outputDirectory.Should().OnlyHaveFiles(expectedFiles);
        }

        [TestMethod]
        public void It_regenerates_the_build_deps_file_when_BundledRuntimeAssetRuntimeIdentifiers_changes()
        {
            const string ProjectName = "TestProjWithPackageDependencies";

            TestProject testProject = new()
            {
                Name = ProjectName,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()));
            testProject.PackageReferences.Add(new TestPackageReference("sqlite", "3.13.0"));

            var testProjectInstance = TestAssetsManager.CreateTestProject(testProject);
            var buildCommand = new BuildCommand(testProjectInstance);

            buildCommand.Execute("/p:Restore=false", "/p:BundledRuntimeAssetRuntimeIdentifiers=linux-x64")
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);
            var depsFilePath = Path.Combine(outputDirectory.FullName, $"{ProjectName}.deps.json");
            using (var deps = JsonDocument.Parse(File.ReadAllText(depsFilePath)))
            {
                var runtimeTargets = deps.RootElement.GetProperty("targets").EnumerateObject().Single().Value
                    .GetProperty("SQLite/3.13.0").GetProperty("runtimeTargets");
                runtimeTargets.EnumerateObject().Select(asset => asset.Name)
                    .Should().ContainSingle(asset => asset.Contains("linux-x64", StringComparison.Ordinal));
            }

            buildCommand.Execute("/p:Restore=false", "/p:BundledRuntimeAssetRuntimeIdentifiers=osx-x64")
                .Should()
                .Pass();

            using (var deps = JsonDocument.Parse(File.ReadAllText(depsFilePath)))
            {
                var runtimeTargets = deps.RootElement.GetProperty("targets").EnumerateObject().Single().Value
                    .GetProperty("SQLite/3.13.0").GetProperty("runtimeTargets");
                runtimeTargets.EnumerateObject().Select(asset => asset.Name)
                    .Should().ContainSingle(asset => asset.Contains("osx-x64", StringComparison.Ordinal));
            }

            buildCommand.Execute("/p:Restore=false")
                .Should()
                .Pass();

            using (var deps = JsonDocument.Parse(File.ReadAllText(depsFilePath)))
            {
                var runtimeTargets = deps.RootElement.GetProperty("targets").EnumerateObject().Single().Value
                    .GetProperty("SQLite/3.13.0").GetProperty("runtimeTargets");
                runtimeTargets.EnumerateObject().Should().HaveCount(4);
            }
        }

        [TestMethod]
        public void It_filters_runtime_assets_to_multiple_BundledRuntimeAssetRuntimeIdentifiers()
        {
            const string ProjectName = "TestProjWithPackageDependencies";

            TestProject testProject = new()
            {
                Name = ProjectName,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            // sqlite package has RID-specific native assets for linux-x64, osx-x64, win7-x64, win7-x86.
            // Setting BundledRuntimeAssetRuntimeIdentifiers to linux-x64;win7-x64 should include linux-x64 and win7-x64 assets only.
            testProject.AdditionalProperties["BundledRuntimeAssetRuntimeIdentifiers"] = "linux-x64;win7-x64";
            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()));
            testProject.PackageReferences.Add(new TestPackageReference("sqlite", "3.13.0"));

            var testProjectInstance = TestAssetsManager
               .CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testProjectInstance);

            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);

            var expectedFiles = new[]
            {
                $"{ProjectName}{Constants.ExeSuffix}",
                $"{ProjectName}.deps.json",
                $"{ProjectName}.dll",
                $"{ProjectName}.pdb",
                $"{ProjectName}.runtimeconfig.json",
                $"{ProjectName}.runtimeconfig.dev.json",
                "Newtonsoft.Json.dll",
                "runtimes/linux-x64/native/libsqlite3.so",
                "runtimes/win7-x64/native/sqlite3.dll",
            };

            outputDirectory.Should().OnlyHaveFiles(expectedFiles);
        }

        [TestMethod]
        public void It_filters_runtime_assets_when_preserving_store_layout_during_publish()
        {
            const string ProjectName = "TestProjWithPackageDependencies";

            TestProject testProject = new()
            {
                Name = ProjectName,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.AdditionalProperties["BundledRuntimeAssetRuntimeIdentifiers"] = "linux-x64";
            testProject.AdditionalProperties["PreserveStoreLayout"] = "true";
            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()));
            testProject.PackageReferences.Add(new TestPackageReference("sqlite", "3.13.0"));

            var testProjectInstance = TestAssetsManager.CreateTestProject(testProject);
            var publishCommand = new PublishCommand(testProjectInstance);

            publishCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks);
            var sqliteAssets = Directory.GetFiles(outputDirectory.FullName, "*sqlite3*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(outputDirectory.FullName, path).Replace(Path.DirectorySeparatorChar, '/'));

            sqliteAssets.Should().ContainSingle(asset =>
                asset.EndsWith("runtimes/linux-x64/native/libsqlite3.so", StringComparison.Ordinal));
        }

        [TestMethod]
        [DataRow("linux-x64", "osx-x64")]
        [DataRow("", "linux-x64")]
        [DataRow("linux-x64", "")]
        [DataRow("linux-x64", "linux-x64")]
        public void It_uses_the_publish_runtime_asset_filter_when_publishing_without_build(string buildFilter, string publishFilter)
        {
            const string ProjectName = "TestProjWithPackageDependencies";

            TestProject testProject = new()
            {
                Name = ProjectName,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.AdditionalProperties["BundledRuntimeAssetRuntimeIdentifiers"] = buildFilter;
            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()));
            testProject.PackageReferences.Add(new TestPackageReference("sqlite", "3.13.0"));

            var testProjectInstance = TestAssetsManager.CreateTestProject(testProject, identifier: $"{buildFilter}-{publishFilter}");
            var buildCommand = new BuildCommand(testProjectInstance);
            buildCommand.Execute()
                .Should()
                .Pass();

            var buildDepsPath = Path.Combine(buildCommand.GetOutputDirectory(testProject.TargetFrameworks).FullName, $"{ProjectName}.deps.json");
            string buildDepsContents = File.ReadAllText(buildDepsPath);

            var publishCommand = new PublishCommand(testProjectInstance) { ShouldRestore = false };
            publishCommand.Execute("/p:NoBuild=true", $"/p:BundledRuntimeAssetRuntimeIdentifiers={publishFilter}")
                .Should()
                .Pass();

            var outputDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks);
            using var deps = JsonDocument.Parse(File.ReadAllText(Path.Combine(outputDirectory.FullName, $"{ProjectName}.deps.json")));
            var runtimeTargets = deps.RootElement.GetProperty("targets").EnumerateObject().Single().Value
                .GetProperty("SQLite/3.13.0").GetProperty("runtimeTargets");
            string[] expectedAssets = publishFilter == ""
                ? ["runtimes/linux-x64/native/libsqlite3.so", "runtimes/osx-x64/native/libsqlite3.dylib",
                   "runtimes/win7-x64/native/sqlite3.dll", "runtimes/win7-x86/native/sqlite3.dll"]
                : publishFilter == "linux-x64"
                    ? ["runtimes/linux-x64/native/libsqlite3.so"]
                    : ["runtimes/osx-x64/native/libsqlite3.dylib"];
            runtimeTargets.EnumerateObject().Select(asset => asset.Name).Should().BeEquivalentTo(expectedAssets);
            Directory.GetFiles(outputDirectory.FullName, "*sqlite3*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(outputDirectory.FullName, path).Replace(Path.DirectorySeparatorChar, '/'))
                .Should().BeEquivalentTo(expectedAssets);
            File.ReadAllText(buildDepsPath).Should().Be(buildDepsContents);
        }
    }
}
