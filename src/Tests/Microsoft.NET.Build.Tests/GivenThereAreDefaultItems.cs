﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Assertions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Xunit;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit.Abstractions;
using NuGet.ProjectModel;
using NuGet.Common;
using NuGet.Frameworks;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThereAreDefaultItems : SdkTest
    {
        public GivenThereAreDefaultItems(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_ignores_excluded_folders()
        {
            Action<GetValuesCommand> setup = getValuesCommand =>
            {
                foreach (string folder in new[] { "bin", "obj", ".vscode" })
                {
                    WriteFile(Path.Combine(getValuesCommand.ProjectRootPath, folder, "source.cs"),
                        "!InvalidCSharp!");
                }

                WriteFile(Path.Combine(getValuesCommand.ProjectRootPath, "Code", "Class1.cs"),
                    "public class Class1 {}");

                WriteFile(Path.Combine(getValuesCommand.ProjectRootPath, "Packages", "Package.cs"),
                    "public class Package {}");
            };

            var compileItems = GivenThatWeWantToBuildALibrary.GetValuesFromTestLibrary(Log, _testAssetsManager, "Compile", setup);

            RemoveGeneratedCompileItems(compileItems);

            var expectedItems = new[]
            {
                "Helper.cs",
                @"Code\Class1.cs",
                @"Packages\Package.cs"
            }
            .Select(item => item.Replace('\\', Path.DirectorySeparatorChar))
            .ToArray();

            compileItems.Should().BeEquivalentTo(expectedItems);
        }

        [Fact]
        public void It_excludes_items_in_a_custom_outputpath()
        {
            Action<GetValuesCommand> setup = getValuesCommand =>
            {

                WriteFile(Path.Combine(getValuesCommand.ProjectRootPath, "Output", "CSharpInOutput.cs"),
                    "!InvalidCSharp!");

                WriteFile(Path.Combine(getValuesCommand.ProjectRootPath, "Code", "Class1.cs"),
                    "public class Class1 {}");
            };

            Action<XDocument> projectChanges = project =>
            {
                var ns = project.Root.Name.Namespace;

                var propertyGroup = new XElement(ns + "PropertyGroup");
                project.Root.Add(propertyGroup);
                propertyGroup.Add(new XElement(ns + "OutputPath", "Output"));
            };


            var compileItems = GivenThatWeWantToBuildALibrary.GetValuesFromTestLibrary(Log, _testAssetsManager, "Compile", setup, projectChanges: projectChanges);

            RemoveGeneratedCompileItems(compileItems);

            var expectedItems = new[]
            {
                "Helper.cs",
                @"Code\Class1.cs"
            }
            .Select(item => item.Replace('\\', Path.DirectorySeparatorChar))
            .ToArray();

            compileItems.Should().BeEquivalentTo(expectedItems);
        }

        [Fact]
        public void It_allows_excluded_folders_to_be_overridden()
        {
            Action<GetValuesCommand> setup = getValuesCommand =>
            {
                foreach (string folder in new[] { "bin", "obj", "packages" })
                {
                    WriteFile(Path.Combine(getValuesCommand.ProjectRootPath, folder, "source.cs"),
                        $"public class ClassFrom_{folder} {{}}");
                }

                WriteFile(Path.Combine(getValuesCommand.ProjectRootPath, "Code", "Class1.cs"),
                    "public class Class1 {}");
            };

            Action<XDocument> projectChanges = project =>
            {
                var ns = project.Root.Name.Namespace;

                project.Root.Element(ns + "PropertyGroup").Add(new XElement(ns + "EnableDefaultItems", "False"));

                XElement itemGroup = new XElement(ns + "ItemGroup");
                project.Root.Add(itemGroup);
                itemGroup.Add(new XElement(ns + "Compile", new XAttribute("Include", "**\\*.cs")));
            };

            var compileItems = GivenThatWeWantToBuildALibrary.GetValuesFromTestLibrary(Log, _testAssetsManager,
                "Compile", setup, new[] { "/p:DisableDefaultRemoves=true" }, GetValuesCommand.ValueType.Item,
                projectChanges: projectChanges);

            RemoveGeneratedCompileItems(compileItems);

            var expectedItems = new[]
            {
                "Helper.cs",
                @"Code\Class1.cs",
                @"bin\source.cs",
                @"obj\source.cs",
                @"packages\source.cs"
            }
            .Select(item => item.Replace('\\', Path.DirectorySeparatorChar))
            .ToArray();

            compileItems.Should().BeEquivalentTo(expectedItems);
        }

        [Fact]
        public void It_allows_items_outside_project_root_to_be_included()
        {
            Action<GetValuesCommand> setup = getValuesCommand =>
            {
                string sharedCodePath = Path.Combine(Path.GetDirectoryName(getValuesCommand.ProjectRootPath), "Shared");
                WriteFile(Path.Combine(sharedCodePath, "Shared.cs"),
                    "public class SharedClass {}");

                WriteFile(Path.Combine(getValuesCommand.ProjectRootPath, "Code", "Class1.cs"),
                    "public class Class1 {}");
            };

            Action<XDocument> projectChanges = project =>
            {
                var ns = project.Root.Name.Namespace;

                XElement itemGroup = new XElement(ns + "ItemGroup");
                project.Root.Add(itemGroup);
                itemGroup.Add(new XElement(ns + "Compile", new XAttribute("Include", "..\\Shared\\**\\*.cs")));
            };

            var compileItems = GivenThatWeWantToBuildALibrary.GetValuesFromTestLibrary(Log, _testAssetsManager, "Compile", setup, projectChanges: projectChanges);

            RemoveGeneratedCompileItems(compileItems);

            var expectedItems = new[]
            {
                "Helper.cs",
                @"Code\Class1.cs",
                @"..\Shared\Shared.cs",
            }
            .Select(item => item.Replace('\\', Path.DirectorySeparatorChar))
            .ToArray();

            compileItems.Should().BeEquivalentTo(expectedItems);
        }

        [Fact]
        public void It_allows_a_project_subfolder_to_be_excluded()
        {
            Action<GetValuesCommand> setup = getValuesCommand =>
            {
                WriteFile(Path.Combine(getValuesCommand.ProjectRootPath, "Code", "Class1.cs"),
                    "public class Class1 {}");
                WriteFile(Path.Combine(getValuesCommand.ProjectRootPath, "Excluded", "Excluded.cs"),
                    "!InvalidCSharp!");
            };

            Action<XDocument> projectChanges = project =>
            {
                var ns = project.Root.Name.Namespace;

                XElement itemGroup = new XElement(ns + "ItemGroup");
                project.Root.Add(itemGroup);
                itemGroup.Add(new XElement(ns + "Compile", new XAttribute("Remove", "Excluded\\**\\*.cs")));
            };

            var compileItems = GivenThatWeWantToBuildALibrary.GetValuesFromTestLibrary(Log, _testAssetsManager, "Compile", setup, projectChanges: projectChanges);

            RemoveGeneratedCompileItems(compileItems);

            var expectedItems = new[]
            {
                "Helper.cs",
                @"Code\Class1.cs",
            }
            .Select(item => item.Replace('\\', Path.DirectorySeparatorChar))
            .ToArray();

            compileItems.Should().BeEquivalentTo(expectedItems);
        }

        [Fact]
        public void It_allows_files_in_the_obj_folder_to_be_explicitly_included()
        {
            Action<GetValuesCommand> setup = getValuesCommand =>
            {
                WriteFile(Path.Combine(getValuesCommand.ProjectRootPath, "Code", "Class1.cs"),
                    "public class Class1 {}");
                WriteFile(Path.Combine(getValuesCommand.ProjectRootPath, "obj", "Class2.cs"),
                    "public class Class2 {}");
                WriteFile(Path.Combine(getValuesCommand.ProjectRootPath, "obj", "Excluded.cs"),
                    "!InvalidCSharp!");
            };

            Action<XDocument> projectChanges = project =>
            {
                var ns = project.Root.Name.Namespace;

                XElement itemGroup = new XElement(ns + "ItemGroup");
                project.Root.Add(itemGroup);
                itemGroup.Add(new XElement(ns + "Compile", new XAttribute("Include", "obj\\Class2.cs")));
            };

            var compileItems = GivenThatWeWantToBuildALibrary.GetValuesFromTestLibrary(Log, _testAssetsManager, "Compile", setup, projectChanges: projectChanges);

            RemoveGeneratedCompileItems(compileItems);

            var expectedItems = new[]
            {
                "Helper.cs",
                @"Code\Class1.cs",
                @"obj\Class2.cs"
            }
            .Select(item => item.Replace('\\', Path.DirectorySeparatorChar))
            .ToArray();

            compileItems.Should().BeEquivalentTo(expectedItems);
        }

        [Fact]
        public void It_allows_a_CSharp_file_to_be_used_as_an_EmbeddedResource()
        {
            Action<GetValuesCommand> setup = getValuesCommand =>
            {
                WriteFile(Path.Combine(getValuesCommand.ProjectRootPath, "Code", "Class1.cs"),
                    "public class Class1 {}");
                WriteFile(Path.Combine(getValuesCommand.ProjectRootPath, "CSharpAsResource.cs"),
                    "public class CSharpAsResource {}");
            };

            Action<XDocument> projectChanges = project =>
            {
                var ns = project.Root.Name.Namespace;

                XElement itemGroup = new XElement(ns + "ItemGroup");
                project.Root.Add(itemGroup);
                itemGroup.Add(new XElement(ns + "EmbeddedResource", new XAttribute("Include", "CSharpAsResource.cs")));
                itemGroup.Add(new XElement(ns + "Compile", new XAttribute("Remove", "CSharpAsResource.cs")));
            };

            var compileItems = GivenThatWeWantToBuildALibrary.GetValuesFromTestLibrary(Log, _testAssetsManager, "Compile", setup, projectChanges: projectChanges);

            RemoveGeneratedCompileItems(compileItems);

            var expectedItems = new[]
            {
                "Helper.cs",
                @"Code\Class1.cs",
            }
            .Select(item => item.Replace('\\', Path.DirectorySeparatorChar))
            .ToArray();

            compileItems.Should().BeEquivalentTo(expectedItems);


            var embeddedResourceItems = GivenThatWeWantToBuildALibrary.GetValuesFromTestLibrary(Log, _testAssetsManager, "EmbeddedResource", setup, projectChanges: projectChanges);

            var expectedEmbeddedResourceItems = new[]
            {
                "CSharpAsResource.cs"
            }
            .Select(item => item.Replace('\\', Path.DirectorySeparatorChar))
            .ToArray();

            embeddedResourceItems.Should().BeEquivalentTo(expectedEmbeddedResourceItems);
        }

        [Fact]
        public void It_allows_a_CSharp_file_to_be_used_as_Content()
        {
            Action<GetValuesCommand> setup = getValuesCommand =>
            {
                WriteFile(Path.Combine(getValuesCommand.ProjectRootPath, "Code", "Class1.cs"),
                    "public class Class1 {}");
                WriteFile(Path.Combine(getValuesCommand.ProjectRootPath, "CSharpAsContent.cs"),
                    "public class CSharpAsContent {}");

                WriteFile(Path.Combine(getValuesCommand.ProjectRootPath, "None.txt"), "Content file");
            };

            Action<XDocument> projectChanges = project =>
            {
                var ns = project.Root.Name.Namespace;

                XElement itemGroup = new XElement(ns + "ItemGroup");
                project.Root.Add(itemGroup);
                itemGroup.Add(new XElement(ns + "Content", new XAttribute("Include", "CSharpAsContent.cs"),
                    new XAttribute("CopyToOutputDirectory", "true")));
                itemGroup.Add(new XElement(ns + "Compile", new XAttribute("Remove", "CSharpAsContent.cs")));
            };

            var compileItems = GivenThatWeWantToBuildALibrary.GetValuesFromTestLibrary(Log, _testAssetsManager, "Compile", setup, projectChanges: projectChanges);

            RemoveGeneratedCompileItems(compileItems);

            var expectedItems = new[]
            {
                "Helper.cs",
                @"Code\Class1.cs",
            }
            .Select(item => item.Replace('\\', Path.DirectorySeparatorChar))
            .ToArray();

            compileItems.Should().BeEquivalentTo(expectedItems);


            var contentItems = GivenThatWeWantToBuildALibrary.GetValuesFromTestLibrary(Log, _testAssetsManager, "Content", setup, projectChanges: projectChanges);

            var expectedContentItems = new[]
            {
                "CSharpAsContent.cs",
            }
            .Select(item => item.Replace('\\', Path.DirectorySeparatorChar))
            .ToArray();

            contentItems.Should().BeEquivalentTo(expectedContentItems);

            var noneItems = GivenThatWeWantToBuildALibrary.GetValuesFromTestLibrary(Log, _testAssetsManager, "None", setup, projectChanges: projectChanges);

            var expectedNoneItems = new[]
            {
                "None.txt"
            }
            .Select(item => item.Replace('\\', Path.DirectorySeparatorChar))
            .ToArray();

            noneItems.Should().BeEquivalentTo(expectedNoneItems);
        }

        [Fact]
        public void It_does_not_include_source_or_resx_files_in_None()
        {
            var testProject = new TestProject()
            {
                Name = "DontIncludeSourceFilesInNone",
                TargetFrameworks = "netcoreapp2.0",
                IsExe = true,
                IsSdkProject = true
            };
            testProject.AdditionalProperties["EnableDefaultCompileItems"] = "false";
            testProject.AdditionalProperties["EnableDefaultResourceItems"] = "false";

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    XElement itemGroup = new XElement(ns + "ItemGroup");
                    project.Root.Add(itemGroup);
                    itemGroup.Add(new XElement(ns + "Compile", new XAttribute("Include", testProject.Name + ".cs")));
                })
                .Restore(Log, testProject.Name);

            var projectFolder = Path.Combine(testAsset.TestRoot, testProject.Name);

            File.WriteAllText(Path.Combine(projectFolder, "ShouldBeIgnored.cs"), "!InvalidCSharp!");
            File.WriteAllText(Path.Combine(projectFolder, "Resources.resx"), "<Resource/>");

            var getCompileItemsCommand = new GetValuesCommand(Log, projectFolder, testProject.TargetFrameworks, "Compile", GetValuesCommand.ValueType.Item);
            getCompileItemsCommand.Execute()
                .Should()
                .Pass();

            var compileItems = getCompileItemsCommand.GetValues();
            RemoveGeneratedCompileItems(compileItems);
            compileItems.ShouldBeEquivalentTo(new[] { testProject.Name + ".cs" });

            var getNoneItemsCommand = new GetValuesCommand(Log, projectFolder, testProject.TargetFrameworks, "None", GetValuesCommand.ValueType.Item);
            getNoneItemsCommand.Execute()
                .Should()
                .Pass();

            getNoneItemsCommand.GetValues()
                .Should().BeEmpty();

            var getResourceItemsCommand = new GetValuesCommand(Log, projectFolder, testProject.TargetFrameworks, "Resource", GetValuesCommand.ValueType.Item);
            getResourceItemsCommand.Execute()
                .Should()
                .Pass();

            getResourceItemsCommand.GetValues()
                .Should().BeEmpty();

        }

        [Fact]
        public void Default_items_have_the_correct_relative_paths()
        {
            Action<XDocument> projectChanges = project =>
            {
                var ns = project.Root.Name.Namespace;

                var propertyGroup = new XElement(ns + "PropertyGroup");
                project.Root.Add(propertyGroup);
                propertyGroup.Add(new XElement(ns + "EnableDefaultContentItems", "false"));

                //  Copy all None items to output directory
                var itemGroup = new XElement(ns + "ItemGroup");
                project.Root.Add(itemGroup);
                itemGroup.Add(new XElement(ns + "None", new XAttribute("Update", "@(None)"), new XAttribute("CopyToOutputDirectory", "PreserveNewest")));
            };

            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibrary")
                .WithSource()
                .WithProjectChanges(projectChanges)
                .Restore(Log, relativePath: "TestLibrary");

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TestLibrary");

            var buildCommand = new BuildCommand(Log, libraryProjectDirectory);

            WriteFile(Path.Combine(buildCommand.ProjectRootPath, "ProjectRoot.txt"), "ProjectRoot");
            WriteFile(Path.Combine(buildCommand.ProjectRootPath, "Subfolder", "ProjectSubfolder.txt"), "ProjectSubfolder");
            WriteFile(Path.Combine(buildCommand.ProjectRootPath, "wwwroot", "wwwroot.txt"), "wwwroot");
            WriteFile(Path.Combine(buildCommand.ProjectRootPath, "wwwroot", "wwwsubfolder", "wwwsubfolder.txt"), "wwwsubfolder");

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory("netstandard1.5");

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "TestLibrary.dll",
                "TestLibrary.pdb",
                "TestLibrary.deps.json",
                "ProjectRoot.txt",
                "Subfolder/ProjectSubfolder.txt",
                "wwwroot/wwwroot.txt",
                "wwwroot/wwwsubfolder/wwwsubfolder.txt",
            });
        }
 
        //  Disable this test on full framework, as generating strong named satellite assemblies with AL.exe requires Admin permissions
        //  See https://github.com/dotnet/sdk/issues/732
        [CoreMSBuildOnlyFact]
        public void Compile_items_can_be_explicitly_specified_while_default_EmbeddedResource_items_are_used()
        {
            Action<XDocument> projectChanges = project =>
            {
                var ns = project.Root.Name.Namespace;

                project.Root.Element(ns + "PropertyGroup").Add(
                    new XElement(ns + "EnableDefaultCompileItems", "false"));

                var itemGroup = new XElement(ns + "ItemGroup");
                project.Root.Add(itemGroup);
                itemGroup.Add(new XElement(ns + "Compile", new XAttribute("Include", "Program.cs")));
            };

            Action<BuildCommand> setup = buildCommand =>
            {
                WriteFile(Path.Combine(buildCommand.ProjectRootPath, "ShouldNotBeCompiled.cs"),
                    "!InvalidCSharp!");
            };

            GivenThatWeWantAllResourcesInSatellite.TestSatelliteResources(Log, _testAssetsManager, projectChanges, setup, "ExplicitCompileDefaultEmbeddedResource");
        }

        [Fact]
        public void It_gives_an_error_message_if_duplicate_compile_items_are_included()
        {
            var testProject = new TestProject()
            {
                Name = "DuplicateCompileItems",
                TargetFrameworks = "netstandard1.6",
                IsSdkProject = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var itemGroup = new XElement(ns + "ItemGroup");
                    project.Root.Add(itemGroup);
                    itemGroup.Add(new XElement(ns + "Compile", new XAttribute("Include", @"**\*.cs")));
                })
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            WriteFile(Path.Combine(buildCommand.ProjectRootPath, "Class1.cs"), "public class Class1 {}");

            buildCommand
                .Execute()
                .Should()
                .Fail()
                .And.HaveStdOutContaining("DuplicateCompileItems.cs")
                .And.HaveStdOutContaining("Class1.cs")
                .And.HaveStdOutContaining("EnableDefaultCompileItems");
        }

        [Fact]
        public void It_gives_the_correct_error_if_duplicate_compile_items_are_included_and_default_items_are_disabled()
        {
            var testProject = new TestProject()
            {
                Name = "DuplicateCompileItems",
                TargetFrameworks = "netstandard1.6",
                IsSdkProject = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, "DuplicateCompileItemsWithDefaultItemsDisabled")
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;

                    project.Root.Element(ns + "PropertyGroup").Add(
                        new XElement(ns + "EnableDefaultCompileItems", "false"));

                    var itemGroup = new XElement(ns + "ItemGroup");
                    project.Root.Add(itemGroup);
                    itemGroup.Add(new XElement(ns + "Compile", new XAttribute("Include", @"**\*.cs")));
                    itemGroup.Add(new XElement(ns + "Compile", new XAttribute("Include", @"DuplicateCompileItems.cs")));
                })
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            WriteFile(Path.Combine(buildCommand.ProjectRootPath, "Class1.cs"), "public class Class1 {}");

            buildCommand
                .Execute()
                .Should()
                .Fail()
                .And.HaveStdOutContaining("DuplicateCompileItems.cs")
                //  Class1.cs wasn't included multiple times, so it shouldn't be mentioned
                .And.NotHaveStdOutMatching("Class1.cs")
                //  Default items weren't enabled, so the error message should come from the C# compiler and shouldn't include the information about default compile items
                .And.HaveStdOutContaining("MSB3105")
                .And.NotHaveStdOutMatching("EnableDefaultCompileItems");
        }

        [Fact]
        public void Implicit_package_references_are_overridden_by_PackageReference_includes_in_the_project_file()
        {
            var testProject = new TestProject()
            {
                //  Underscore is in the project name so we can verify that the warning message output contained "PackageReference"
                Name = "DeduplicatePackage_Reference",
                TargetFrameworks = "netstandard1.6",
                IsSdkProject = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, "DeduplicatePackage_Reference")
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;

                    //  Set the implicit package reference version to something that doesn't exist to verify that the Include
                    //  in the project file overrides the implicit one
                    project.Root.Element(ns + "PropertyGroup").Add(
                        new XElement(ns + "NetStandardImplicitPackageVersion", "0.1.0-does-not-exist"));

                    var itemGroup = new XElement(ns + "ItemGroup");
                    project.Root.Add(itemGroup);

                    //  Use non-standard casing for the explicit package reference to verify that comparison is case-insensitive
                    itemGroup.Add(new XElement(ns + "PackageReference",
                        new XAttribute("Include", "netstandard.Library"), new XAttribute("Version", "1.6.1")));
                })
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute()
                .Should()
                .Pass()
                .And.HaveStdOutContaining("PackageReference")
                .And.HaveStdOutContaining("'NETStandard.Library'");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Implicit_NetCoreApp_reference_can_be_overridden(bool disableImplicitFrameworkReferences)
        {
            var testProject = new TestProject()
            {
                Name = "OverrideNetCoreApp",
                TargetFrameworks = "netcoreapp2.0",
                IsSdkProject = true,
                IsExe = true
            };

            if (disableImplicitFrameworkReferences)
            {
                testProject.AdditionalProperties["DisableImplicitFrameworkReferences"] = "true";
            }

            string explicitPackageVersion = "2.0.3";
            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.NETCore.App", explicitPackageVersion));

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: disableImplicitFrameworkReferences.ToString())
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute()
                .Should()
                .Pass()
                .And
                .NotHaveStdOutContaining("NETSDK1071");
                ;

            LockFile lockFile = LockFileUtilities.GetLockFile(Path.Combine(buildCommand.ProjectRootPath, "obj", "project.assets.json"), NullLogger.Instance);

            var target = lockFile.GetTarget(NuGetFramework.Parse(testProject.TargetFrameworks), null);
            var netCoreAppLibrary = target.Libraries.Single(l => l.Name == "Microsoft.NETCore.App");
            netCoreAppLibrary.Version.ToString().Should().Be(explicitPackageVersion);
        }
        
        void RemoveGeneratedCompileItems(List<string> compileItems)
        {
            //  Remove auto-generated compile items.
            //  TargetFrameworkAttribute comes from %TEMP%\{TargetFrameworkMoniker}.AssemblyAttributes.cs
            //  Other default attributes generated by .NET SDK (for example AssemblyDescriptionAttribute and AssemblyTitleAttribute) come from
            //      { AssemblyName}.AssemblyInfo.cs in the intermediate output path
            var itemsToRemove = compileItems.Where(i =>
                    i.EndsWith("AssemblyAttributes.cs", System.StringComparison.OrdinalIgnoreCase) ||
                    i.EndsWith("AssemblyInfo.cs", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var itemToRemove in itemsToRemove)
            {
                compileItems.Remove(itemToRemove);
            }
        }

        private void WriteFile(string path, string contents)
        {
            string folder = Path.GetDirectoryName(path);
            Directory.CreateDirectory(folder);
            File.WriteAllText(path, contents);
        }
    }
}
