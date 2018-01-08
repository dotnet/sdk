// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using NuGet.Packaging;

namespace Microsoft.NET.ToolPack.Tests
{
    public class GivenThatWeWantToPackAToolProject : SdkTest
    {
        private readonly string _nugetPackage;

        public GivenThatWeWantToPackAToolProject(ITestOutputHelper log) : base(log)
        {
            TestAsset helloWorldAsset = _testAssetsManager
                .CopyTestAsset("PortableTool", "PackPortableTool" + Path.GetRandomFileName()) //TODO remvoe that, no checkin 
                .WithSource()
                .Restore(Log);

            var packCommand = new PackCommand(Log, helloWorldAsset.TestRoot);

            packCommand.Execute();

            _nugetPackage = packCommand.GetNuGetPackage();
        }

        [Fact]
        public void It_packs_successfully()
        {
            using (var nupkgReader = new PackageArchiveReader(_nugetPackage))
            {
                nupkgReader
                    .GetToolItems()
                    .Should().NotBeEmpty();
            }
        }

        [Fact]
        public void It_finds_the_entry_point_dll_and_command_name_and_put_in_setting_file()
        {
            using (var nupkgReader = new PackageArchiveReader(_nugetPackage))
            {
                IEnumerable<NuGet.Frameworks.NuGetFramework> supportedFrameworks = nupkgReader.GetSupportedFrameworks();
                supportedFrameworks.Should().NotBeEmpty();

                var tmpfilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                string copiedFile = nupkgReader.ExtractFile($"tools/DotnetToolSettings.xml", tmpfilePath, null);
                File.ReadAllText(copiedFile)
                    .Should()
                    .Contain("consoledemo.dll", "it should contain entry point dll that is same as the msbuild well known properties $(TargetFileName)")
                    .And
                    .Contain("consoledemo", "it should contain command name that is same as the msbuild well known properties $(TargetName)");
            }
        }

        [Fact]
        public void It_adds_platform_package_to_dependency_and_remove_other_package_dependency()
        {
            using (var nupkgReader = new PackageArchiveReader(_nugetPackage))
            {
                nupkgReader
                    .GetPackageDependencies().First().Packages
                    .Should().OnlyContain(p => p.Id == "Microsoft.NETCore.Platforms");
            }
        }

        [Fact]
        public void It_contains_runtimeconfig_for_each_tfm()
        {
            using (var nupkgReader = new PackageArchiveReader(_nugetPackage))
            {
                IEnumerable<NuGet.Frameworks.NuGetFramework> supportedFrameworks = nupkgReader.GetSupportedFrameworks();
                supportedFrameworks.Should().NotBeEmpty();

                foreach (NuGet.Frameworks.NuGetFramework framework in supportedFrameworks)
                {
                    if (framework.GetShortFolderName() == "any")
                    {
                        continue;
                    }

                    var allItems = nupkgReader.GetToolItems().SelectMany(i => i.Items).ToList();
                    allItems.Should().Contain($"tools/{framework.GetShortFolderName()}/any/consoledemo.runtimeconfig.json");
                }
            }
        }

        [Fact]
        public void It_does_not_contain_lib()
        {
            using (var nupkgReader = new PackageArchiveReader(_nugetPackage))
            {
                nupkgReader.GetLibItems().Should().BeEmpty();
            }
        }

        [Fact]
        public void It_contains_folder_structure_tfm_any()
        {
            using (var nupkgReader = new PackageArchiveReader(_nugetPackage))
            {
                nupkgReader
                    .GetToolItems()
                    .Should().Contain(
                        f => f.Items.
                            Contains($"tools/{f.TargetFramework.GetShortFolderName()}/any/consoledemo.dll"));
            }
        }

        [Fact]
        public void It_contains_packagetype_dotnettool()
        {
            using (var nupkgReader = new PackageArchiveReader(_nugetPackage))
            {
                nupkgReader
                    .GetPackageTypes().Should().ContainSingle(t => t.Name == "DotnetTool");
            }
        }

        [Fact]
        public void It_contains_dependencies_dll()
        {
            using (var nupkgReader = new PackageArchiveReader(_nugetPackage))
            {
                IEnumerable<NuGet.Frameworks.NuGetFramework> supportedFrameworks = nupkgReader.GetSupportedFrameworks();
                supportedFrameworks.Should().NotBeEmpty();

                foreach (NuGet.Frameworks.NuGetFramework framework in supportedFrameworks)
                {
                    if (framework.GetShortFolderName() == "any")
                    {
                        continue;
                    }

                    var allItems = nupkgReader.GetToolItems().SelectMany(i => i.Items).ToList();
                    allItems.Should().Contain($"tools/{framework.GetShortFolderName()}/any/Newtonsoft.Json.dll");
                }
            }
        }
    }
}
