﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Microsoft.NET.TestFramework.ProjectConstruction;
using System.Xml.Linq;
using System.Linq;
using FluentAssertions;
using System.Runtime.InteropServices;
using Xunit.Abstractions;

namespace Microsoft.NET.Pack.Tests
{
    public class GivenThatThereAreImplicitPackageReferences : SdkTest
    {
        public GivenThatThereAreImplicitPackageReferences(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void Packing_a_netstandard_1_x_library_includes_the_implicit_dependency()
        {
            TestProject testProject = new TestProject()
            {
                Name = "PackNetStandard1x",
                IsSdkProject = true,
                TargetFrameworks = "netstandard1.4",
                IsExe = false
            };

            var dependencies = PackAndGetDependencies(testProject);

            dependencies.Count().Should().Be(1);
            dependencies.Single().Attribute("id").Value
                .Should().Be("NETStandard.Library");
            dependencies.Single().Attribute("version").Value
                .Should().Be("1.6.1");
        }

        [Fact]
        public void Packing_a_netstandard_2_0_library_does_not_include_the_implicit_dependency()
        {
            TestProject testProject = new TestProject()
            {
                Name = "PackNetStandard20",
                IsSdkProject = true,
                TargetFrameworks = "netstandard2.0",
                IsExe = false
            };

            var dependencies = PackAndGetDependencies(testProject);

            dependencies.Should().BeEmpty();
        }

        [Fact]
        public void Packing_a_netcoreapp_1_1_library_includes_the_implicit_dependency()
        {
            TestProject testProject = new TestProject()
            {
                Name = "PackNetCoreApp11Library",
                IsSdkProject = true,
                TargetFrameworks = "netcoreapp1.1",
                IsExe = false
            };

            var dependencies = PackAndGetDependencies(testProject);

            dependencies.Count().Should().Be(1);
            dependencies.Single().Attribute("id").Value
                .Should().Be("Microsoft.NETCore.App");

            //  Don't check the exact version so that the test doesn't break if we roll forward to new patch versions of the package
            dependencies.Single().Attribute("version").Value
                .Should().StartWith("1.1.");
        }

        [Fact]
        public void Packing_a_netcoreapp_2_0_library_does_not_include_the_implicit_dependency()
        {
            TestProject testProject = new TestProject()
            {
                Name = "PackNetCoreApp20Library",
                IsSdkProject = true,
                TargetFrameworks = "netcoreapp2.0",
                IsExe = false
            };

            var dependencies = PackAndGetDependencies(testProject);

            dependencies.Should().BeEmpty();
        }

        [Fact]
        public void Packing_a_netcoreapp_1_1_app_includes_the_implicit_dependency()
        {
            TestProject testProject = new TestProject()
            {
                Name = "PackNetCoreApp11App",
                IsSdkProject = true,
                TargetFrameworks = "netcoreapp1.1",
                IsExe = true
            };

            var dependencies = PackAndGetDependencies(testProject);

            dependencies.Count().Should().Be(1);
            dependencies.Single().Attribute("id").Value
                .Should().Be("Microsoft.NETCore.App");

            //  Don't check the exact version so that the test doesn't break if we roll forward to new patch versions of the package
            dependencies.Single().Attribute("version").Value
                .Should().StartWith("1.1.");
        }

        [Fact]
        public void Packing_an_app_exclude_dependencys_framework_assemblies_dependency()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }
            TestProject testProject = new TestProject()
            {
                Name = "PackNet461App",
                IsSdkProject = true,
                TargetFrameworks = "net461",
            };

            testProject.PackageReferences.Add(
                new TestPackageReference(
                    "System.IO.Compression",
                    "4.3.0",
                    null));
            testProject.References.Add("System.Web");

            var dependencies = GetFrameworkAssemblies(PackAndGetNuspec(testProject), out var _);
            
            dependencies.Count().Should().Be(1);
            dependencies.Single().Attribute("assemblyName").Value.Should().Be("System.Web");
        }

        //  Disabled on full framework MSBuild until CI machines have VS with bundled .NET Core / .NET Standard versions
        //  See https://github.com/dotnet/sdk/issues/1077
        [CoreMSBuildOnlyFact]
        public void Packing_a_netcoreapp_2_0_app_includes_the_implicit_dependency()
        {
            TestProject testProject = new TestProject()
            {
                Name = "PackNetCoreApp20App",
                IsSdkProject = true,
                TargetFrameworks = "netcoreapp2.0",
                IsExe = true
            };

            var dependencies = PackAndGetDependencies(testProject);

            dependencies.Count().Should().Be(1);
            dependencies.Single().Attribute("id").Value
                .Should().Be("Microsoft.NETCore.App");

            //  Don't check the exact version so that the test doesn't break if we roll forward to new patch versions of the package
            dependencies.Single().Attribute("version").Value
                .Should().StartWith("2.0.");
        }

        [Fact]
        public void Packing_a_multitargeted_library_includes_implicit_dependencies_when_appropriate()
        {
            TestProject testProject = new TestProject()
            {
                Name = "PackMultiTargetedLibrary",
                IsSdkProject = true,
                TargetFrameworks = "netstandard1.1;netstandard2.0;netcoreapp1.1;netcoreapp2.0",
                IsExe = false
            };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                testProject.TargetFrameworks += ";net461";
            }

            var dependencyGroups = GetDependencyGroups(PackAndGetNuspec(testProject), out var ns);

            void ExpectDependencyGroup(string targetFramework, string dependencyId)
            {
                var matchingGroups = dependencyGroups.Where(dg => dg.Attribute("targetFramework").Value == targetFramework).ToList();
                matchingGroups.Count().Should().Be(1);

                var dependencies = matchingGroups.Single().Elements(ns + "dependency");
                if (dependencyId == null)
                {
                    dependencies.Should().BeEmpty();
                }
                else
                {
                    dependencies.Count().Should().Be(1);
                    dependencies.Single().Attribute("id").Value
                        .Should().Be(dependencyId);
                }
            }

            ExpectDependencyGroup(".NETStandard1.1", "NETStandard.Library");
            ExpectDependencyGroup(".NETStandard2.0", null);
            ExpectDependencyGroup(".NETCoreApp1.1", "Microsoft.NETCore.App");
            ExpectDependencyGroup(".NETCoreApp2.0", null);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ExpectDependencyGroup(".NETFramework4.6.1", null);
            }
        }

        private List<XElement> GetDependencyGroups(XDocument nuspec, out XNamespace ns)
        {
            ns = nuspec.Root.Name.Namespace;

            var dependencyGroups = nuspec.Root
                .Element(ns + "metadata")
                .Element(ns + "dependencies")
                .Elements()
                .ToList();

            return dependencyGroups;
        }

        private List<XElement> GetFrameworkAssemblies(XDocument nuspec, out XNamespace ns)
        {
            ns = nuspec.Root.Name.Namespace;

            return nuspec.Root
                .Element(ns + "metadata")
                .Element(ns + "frameworkAssemblies")
                .Elements()
                .ToList();
        }

        private XDocument PackAndGetNuspec(TestProject testProject)
        {
            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject, testProject.Name)
                .Restore(Log, testProject.Name);

            var packCommand = new PackCommand(Log, testProjectInstance.TestRoot, testProject.Name);

            packCommand.Execute()
                .Should()
                .Pass();

            string nuspecPath = packCommand.GetIntermediateNuspecPath();
            var nuspec = XDocument.Load(nuspecPath);
            return nuspec;
        }

        private List<XElement> PackAndGetDependencies(TestProject testProject)
        {
            var dependencyGroups = GetDependencyGroups(PackAndGetNuspec(testProject), out var ns);

            //  There should be only one dependency group for these tests
            dependencyGroups.Count().Should().Be(1);

            //  It should have the right element name
            dependencyGroups.Single().Name.Should().Be(ns + "group");

            var dependencies = dependencyGroups.Single().Elements(ns + "dependency").ToList();

            return dependencies;
        }

        private List<XElement> PackAndGetFrameworkAssemblies(TestProject testProject)
        {
            var frameworkAssemblies = GetFrameworkAssemblies(PackAndGetNuspec(testProject), out var ns);

            //  There should be only one dependency group for these tests
            frameworkAssemblies.Count().Should().Be(1);

            frameworkAssemblies.Should().Contain(f => f.Name == "frameworkAssembly");

            return frameworkAssemblies.Elements(ns + "frameworkAssembly").ToList();
        }
    }
}
