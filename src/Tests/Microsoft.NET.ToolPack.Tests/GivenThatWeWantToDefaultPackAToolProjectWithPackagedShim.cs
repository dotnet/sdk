// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using NuGet.Packaging;
using NuGet.Frameworks;
using fixture = Microsoft.NET.ToolPack.Tests.DefaultPackWithShimsAndResultNugetPackageNuGetPackagexFixture;

namespace Microsoft.NET.ToolPack.Tests
{
    public class GivenThatWeWantToDefaultPackAToolProjectWithPackagedShim : SdkTest, IClassFixture<fixture>
    {
        fixture _fixture;

        public GivenThatWeWantToDefaultPackAToolProjectWithPackagedShim(fixture fixture, ITestOutputHelper log) : base(log)
        {
            fixture.Init(log, _testAssetsManager);
            _fixture = fixture;
        }

        [Theory]
        [InlineData(true, "netcoreapp2.1")]
        [InlineData(false, "netcoreapp2.1")]
        [InlineData(true, "netcoreapp3.0")]
        [InlineData(false, "netcoreapp3.0")]
        public void It_packs_successfully(bool multiTarget, string targetFramework)
        {
            var nugetPackage = _fixture.GetAsset(multiTarget, targetFramework: targetFramework);
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                nupkgReader
                    .GetToolItems()
                    .Should().NotBeEmpty();
            }
        }

        [Theory]
        [InlineData(true, "netcoreapp2.1")]
        [InlineData(false, "netcoreapp2.1")]
        [InlineData(true, "netcoreapp3.0")]
        [InlineData(false, "netcoreapp3.0")]
        public void It_contains_dependencies_dll(bool multiTarget, string targetFramework)
        {
            var nugetPackage = _fixture.GetAsset(multiTarget, targetFramework: targetFramework);
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                IEnumerable<NuGetFramework> supportedFrameworks = nupkgReader.GetSupportedFrameworks();
                supportedFrameworks.Should().NotBeEmpty();

                foreach (NuGetFramework framework in supportedFrameworks)
                {
                    var allItems = nupkgReader.GetToolItems().SelectMany(i => i.Items).ToList();
                    allItems.Should().Contain($"tools/{framework.GetShortFolderName()}/any/Newtonsoft.Json.dll");
                }
            }
        }

        [Theory]
        [InlineData(true, "netcoreapp2.1")]
        [InlineData(false, "netcoreapp2.1")]
        [InlineData(true, "netcoreapp3.0")]
        [InlineData(false, "netcoreapp3.0")]
        public void It_contains_shim(bool multiTarget, string targetFramework)
        {
            var nugetPackage = _fixture.GetAsset(multiTarget, targetFramework: targetFramework);
            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                IEnumerable<NuGetFramework> supportedFrameworks = nupkgReader.GetSupportedFrameworks();
                supportedFrameworks.Should().NotBeEmpty();

                foreach (NuGetFramework framework in supportedFrameworks)
                {
                    var allItems = nupkgReader.GetToolItems().SelectMany(i => i.Items).ToList();
                    allItems.Should().Contain($"tools/{framework.GetShortFolderName()}/any/shims/win-x64/{fixture._customToolCommandName}.exe",
                        "Name should be the same as the command name even customized");
                    allItems.Should().Contain($"tools/{framework.GetShortFolderName()}/any/shims/osx.10.12-x64/{fixture._customToolCommandName}",
                        "RID should be the exact match of the RID in the property, even Apphost only has version of win, osx and linux");
                }
            }
        }
    }
}
