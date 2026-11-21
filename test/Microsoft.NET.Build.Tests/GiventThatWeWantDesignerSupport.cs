// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Extensions.DependencyModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.NET.Build.Tests
{
    [TestClass]
    public class GivenThatWeWantDesignerSupport : SdkTest
    {

        [TestMethod]
        [DataRow("net46", "false")]
        public void It_provides_runtime_configuration_and_shadow_copy_files_via_outputgroup_net46(string targetFramework, string isSelfContained)
        {
            RunDesignerSupportTest(targetFramework, isSelfContained);
        }

        [TestMethod]
        [DataRow("netcoreapp3.0", "true")]
        [DataRow("netcoreapp3.0", "false")]
        [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
        public void It_provides_runtime_configuration_and_shadow_copy_files_via_outputgroup_netcore(string targetFramework, string isSelfContained)
        {
            //  https://github.com/dotnet/sdk/issues/49665
            //  error NETSDK1084: There is no application host available for the specified RuntimeIdentifier 'osx-arm64'.
            RunDesignerSupportTest(targetFramework, isSelfContained);
        }

        [TestMethod]
        [DataRow("net6.0-windows", "true")]
        [DataRow("net6.0-windows", "false")]
        [DataRow("net7.0-windows10.0.17763", "true")]
        [DataRow("net7.0-windows10.0.17763", "false")]
        [OSCondition(OperatingSystems.Windows)]
        public void It_provides_runtime_configuration_and_shadow_copy_files_via_outputgroup_windows(string targetFramework, string isSelfContained)
        {
            RunDesignerSupportTest(targetFramework, isSelfContained);
        }

        private void RunDesignerSupportTest(string targetFramework, string isSelfContained)
        {

            var projectRef = new TestProject
            {
                Name = "ReferencedProject",
                TargetFrameworks = targetFramework,
            };

            var project = new TestProject
            {
                Name = "DesignerTest",
                IsExe = true,
                TargetFrameworks = targetFramework,
                PackageReferences = { new TestPackageReference("NewtonSoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()) },
                ReferencedProjects = { projectRef },
                SelfContained = isSelfContained
            };

            if (targetFramework == "net7.0-windows10.0.17763")
            {
                // Temporary until new projections flow to tests
                project.AdditionalProperties["WindowsSdkPackageVersion"] = "10.0.17763.38";
            }

            var asset = TestAssetsManager
                .CreateTestProject(project, identifier: targetFramework);

            var command = new GetValuesCommand(
                Log,
                Path.Combine(asset.Path, project.Name),
                targetFramework,
                "DesignerRuntimeImplementationProjectOutputGroupOutput",
                GetValuesCommand.ValueType.Item)
            {
                DependsOnTargets = "DesignerRuntimeImplementationProjectOutputGroup",
                MetadataNames = { "TargetPath" },
            };

            command.Execute().Should().Pass();

            var items =
                from item in command.GetValuesWithMetadata()
                select new
                {
                    Identity = item.value,
                    TargetPath = item.metadata["TargetPath"]
                };

            string depsFile = null;
            string runtimeConfig = null;
            var otherFiles = new List<string>();

            foreach (var item in items)
            {
                Path.IsPathFullyQualified(item.Identity).Should().BeTrue();
                Path.GetFileName(item.Identity).Should().Be(item.TargetPath);

                switch (item.TargetPath)
                {
                    case "DesignerTest.designer.deps.json":
                        depsFile = item.Identity;
                        break;
                    case "DesignerTest.designer.runtimeconfig.json":
                        runtimeConfig = item.Identity;
                        break;
                    default:
                        otherFiles.Add(item.TargetPath);
                        break;
                }
            }

            switch (targetFramework)
            {
                case "netcoreapp3.0":
                case "net6.0-windows":
                case "net7.0-windows10.0.17763":
                    var depsFileLibraries = GetRuntimeLibraryFileNames(depsFile);
                    depsFileLibraries.Should().BeEquivalentTo(new[] { "Newtonsoft.Json.dll" });

                    var options = GetRuntimeOptions(runtimeConfig);
                    options["configProperties"]["Microsoft.NETCore.DotNetHostPolicy.SetAppPaths"].Value<bool>().Should().BeTrue();
                    // runtimeconfiguration should not have platform.
                    // it should be net6.0 instead of net6.0-windows
                    options["tfm"].Value<string>().Should().Be(targetFramework.Split('-')[0]);
                    options["additionalProbingPaths"].Value<JArray>().Should().NotBeEmpty();

                    if (targetFramework == "net7.0-windows10.0.17763")
                    {
                        otherFiles.Should().BeEquivalentTo(["ReferencedProject.dll", "ReferencedProject.pdb", "Microsoft.Windows.SDK.NET.dll", "WinRT.Runtime.dll"]);
                    }
                    else
                    {
                        otherFiles.Should().BeEquivalentTo(["ReferencedProject.dll", "ReferencedProject.pdb"]);
                    }

                    break;

                case "net46":
                    depsFile.Should().BeNull();
                    runtimeConfig.Should().BeNull();
                    otherFiles.Should().BeEmpty();
                    break;
            }
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public void It_does_not_include_framework_assets_when_multitargeting_framework_and_core()
        {
            var projectRef = new TestProject
            {
                Name = "ReferencedProject",
                TargetFrameworks = "net6.0-windows;net46",
            };

            var project = new TestProject
            {
                Name = "MultiTargetDesignerTest",
                IsExe = true,
                TargetFrameworks = "net6.0-windows;net46",
                PackageReferences = { new TestPackageReference("NewtonSoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()) },
                ReferencedProjects = { projectRef },
            };

            var asset = TestAssetsManager.CreateTestProject(project);

            (string DepsFile, string RuntimeConfig, List<string> OtherFiles) QueryOutputGroup(string targetFramework)
            {
                var command = new GetValuesCommand(
                    Log,
                    Path.Combine(asset.Path, project.Name),
                    targetFramework,
                    "DesignerRuntimeImplementationProjectOutputGroupOutput",
                    GetValuesCommand.ValueType.Item)
                {
                    DependsOnTargets = "DesignerRuntimeImplementationProjectOutputGroup",
                    MetadataNames = { "TargetPath" },
                };

                command.Execute().Should().Pass();

                string depsFile = null;
                string runtimeConfig = null;
                var otherFiles = new List<string>();

                foreach (var item in command.GetValuesWithMetadata())
                {
                    var targetPath = item.metadata["TargetPath"];
                    switch (targetPath)
                    {
                        case var _ when targetPath.EndsWith(".designer.deps.json"):
                            depsFile = item.value;
                            break;
                        case var _ when targetPath.EndsWith(".designer.runtimeconfig.json"):
                            runtimeConfig = item.value;
                            break;
                        default:
                            otherFiles.Add(targetPath);
                            break;
                    }
                }

                return (depsFile, runtimeConfig, otherFiles);
            }

            var coreResult = QueryOutputGroup("net6.0-windows");
            coreResult.DepsFile.Should().NotBeNull();
            coreResult.RuntimeConfig.Should().NotBeNull();
            coreResult.OtherFiles.Should().BeEquivalentTo(["ReferencedProject.dll", "ReferencedProject.pdb"]);

            var frameworkResult = QueryOutputGroup("net46");
            frameworkResult.DepsFile.Should().BeNull();
            frameworkResult.RuntimeConfig.Should().BeNull();
            frameworkResult.OtherFiles.Should().BeEmpty();
        }

        [TestMethod]
        public void It_includes_nuget_assets_for_framework_when_out_of_proc_designer_is_opted_in()
        {
            var projectRef = new TestProject
            {
                Name = "ReferencedProject",
                TargetFrameworks = "net46",
            };

            var project = new TestProject
            {
                Name = "OopDesignerFrameworkTest",
                IsExe = true,
                TargetFrameworks = "net46",
                PackageReferences = { new TestPackageReference("NewtonSoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()) },
                ReferencedProjects = { projectRef },
            };
            project.AdditionalProperties["UseWinFormsOutOfProcDesigner"] = "true";

            var asset = TestAssetsManager.CreateTestProject(project);

            var command = new GetValuesCommand(
                Log,
                Path.Combine(asset.Path, project.Name),
                "net46",
                "DesignerRuntimeImplementationProjectOutputGroupOutput",
                GetValuesCommand.ValueType.Item)
            {
                DependsOnTargets = "DesignerRuntimeImplementationProjectOutputGroup",
                MetadataNames = { "TargetPath" },
            };

            command.Execute().Should().Pass();

            var targetPaths = command.GetValuesWithMetadata().Select(item => item.metadata["TargetPath"]);

            targetPaths.Should().BeEquivalentTo(["Newtonsoft.Json.dll", "ReferencedProject.dll", "ReferencedProject.pdb"]);
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public void It_does_not_include_framework_assets_when_multitargeting_with_out_of_proc_designer_opted_in_for_all_frameworks()
        {
            var projectRef = new TestProject
            {
                Name = "ReferencedProject",
                TargetFrameworks = "net8.0-windows;net48",
            };

            var project = new TestProject
            {
                Name = "MultiTargetOopDesignerTest",
                IsExe = true,
                TargetFrameworks = "net8.0-windows;net48",
                PackageReferences = { new TestPackageReference("NewtonSoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()) },
                ReferencedProjects = { projectRef },
            };
            // Opt in to the out-of-process designer for every TargetFramework, including net48.
            project.AdditionalProperties["UseWinFormsOutOfProcDesigner"] = "true";

            var asset = TestAssetsManager.CreateTestProject(project);

            (string DepsFile, string RuntimeConfig, List<string> OtherFiles) QueryOutputGroup(string targetFramework)
            {
                var command = new GetValuesCommand(
                    Log,
                    Path.Combine(asset.Path, project.Name),
                    targetFramework,
                    "DesignerRuntimeImplementationProjectOutputGroupOutput",
                    GetValuesCommand.ValueType.Item)
                {
                    DependsOnTargets = "DesignerRuntimeImplementationProjectOutputGroup",
                    MetadataNames = { "TargetPath" },
                };

                command.Execute().Should().Pass();

                string depsFile = null;
                string runtimeConfig = null;
                var otherFiles = new List<string>();

                foreach (var item in command.GetValuesWithMetadata())
                {
                    var targetPath = item.metadata["TargetPath"];
                    switch (targetPath)
                    {
                        case var _ when targetPath.EndsWith(".designer.deps.json"):
                            depsFile = item.value;
                            break;
                        case var _ when targetPath.EndsWith(".designer.runtimeconfig.json"):
                            runtimeConfig = item.value;
                            break;
                        default:
                            otherFiles.Add(targetPath);
                            break;
                    }
                }

                return (depsFile, runtimeConfig, otherFiles);
            }

            var coreResult = QueryOutputGroup("net8.0-windows");
            coreResult.DepsFile.Should().NotBeNull();
            coreResult.RuntimeConfig.Should().NotBeNull();
            coreResult.OtherFiles.Should().BeEquivalentTo(["ReferencedProject.dll", "ReferencedProject.pdb"]);

            var frameworkResult = QueryOutputGroup("net48");
            frameworkResult.DepsFile.Should().BeNull();
            frameworkResult.RuntimeConfig.Should().BeNull();
            frameworkResult.OtherFiles.Should().BeEmpty();
        }

        private static JToken GetRuntimeOptions(string runtimeConfigFilePath)
        {
            var config = ParseRuntimeConfig(runtimeConfigFilePath);
            return config["runtimeOptions"];
        }

        private static IEnumerable<string> GetRuntimeLibraryFileNames(string depsFilePath)
        {
            var deps = ParseDepsFile(depsFilePath);

            return deps.RuntimeLibraries
                       .SelectMany(r => r.RuntimeAssemblyGroups)
                       .SelectMany(a => a.AssetPaths)
                       .Select(p => Path.GetFileName(p));
        }

        private static JToken ParseRuntimeConfig(string path)
        {
            using (var streamReader = File.OpenText(path))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                return JObject.Load(jsonReader);
            }
        }

        private static DependencyContext ParseDepsFile(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var reader = new DependencyContextJsonReader())
            {
                return reader.Read(stream);
            }
        }
    }
}
