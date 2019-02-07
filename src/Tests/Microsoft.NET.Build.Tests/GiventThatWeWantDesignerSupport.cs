// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyModel;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantDesignerSupport : SdkTest
    {
        public GivenThatWeWantDesignerSupport(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("net46")]
        [InlineData("netcoreapp3.0")]
        public void It_provides_runtime_configuration_and_shadow_copy_files_via_outputgroup(string targetFramework)
        {
            var projectRef = new TestProject
            {
                Name = "ReferencedProject",
                TargetFrameworks = targetFramework,
                IsSdkProject = true,
            };

            var project = new TestProject
            {
                Name = "DesignerTest",
                IsExe = true,
                TargetFrameworks = targetFramework,
                IsSdkProject = true,
                PackageReferences = { new TestPackageReference("NewtonSoft.Json", "12.0.1") },
                ReferencedProjects = { projectRef }
            };

            var asset = _testAssetsManager
                .CreateTestProject(project, identifier: targetFramework)
                .Restore(Log, project.Name);

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
                    var depsFileLibraries = GetRuntimeLibraryFileNames(depsFile);
                    depsFileLibraries.Should().BeEquivalentTo(new[] { "Newtonsoft.Json.dll" });
                    
                    var options = GetRuntimeOptions(runtimeConfig);
                    options["configProperties"]["Microsoft.NETCore.DotNetHostPolicy.SetAppPaths"].Value<bool>().Should().BeTrue();
                    options["tfm"].Value<string>().Should().Be(targetFramework);
                    options["additionalProbingPaths"].Value<JArray>().Should().NotBeEmpty();

                    otherFiles.Should().BeEquivalentTo(new[] { "ReferencedProject.dll", "ReferencedProject.pdb" });
                    break;

                case "net46":
                    depsFile.Should().BeNull();
                    runtimeConfig.Should().BeNull();
                    otherFiles.Should().BeEquivalentTo(new[] { "Newtonsoft.Json.dll", "ReferencedProject.dll", "ReferencedProject.pdb" });
                    break;
            }
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
