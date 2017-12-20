// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
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
                .CopyTestAsset("PortableTool", "PackPortableTool")
                .WithSource()
                .Restore(Log);

            var packCommand = new PackCommand(Log, helloWorldAsset.TestRoot);

            packCommand
                .Execute()
                .Should()
                .Pass();

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

        [Fact(Skip = "Pending")]
        public void It_can_find_the_entry_point_dll_and_put_in_setting_file()
        {
        }

        [Fact]
        public void It_contains_runtimeconfigfor_each_tfm()
        {
            using (var nupkgReader = new PackageArchiveReader(_nugetPackage))
            {
                IEnumerable<NuGet.Frameworks.NuGetFramework> supportedFrameworks = nupkgReader.GetSupportedFrameworks();
                supportedFrameworks.Should().NotBeEmpty();

                foreach (NuGet.Frameworks.NuGetFramework framework in supportedFrameworks)
                {
                    nupkgReader
                    .GetToolItems()
                    .Should().Contain(
                        f => f.Items.
                            Contains($"tools/{framework.GetShortFolderName()}/any/consoledemo.runtimeconfig.json"));

                }
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

        [Fact(Skip = "Pending")]
        public void It_contains_packagetype_dotnettool()
        { }

        [Fact(Skip = "Pending")]
        public void It_contains_dependencies_dll()
        { }

        [Fact(Skip = "Pending")]
        public void It_can_use_filename_contain_main_and_put_in_setting_file_as_commandname()
        { }
    }
}
