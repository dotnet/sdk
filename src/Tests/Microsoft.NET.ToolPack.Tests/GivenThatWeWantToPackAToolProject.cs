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
using NuGet.Packaging.Core;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using NuGet.Packaging;

namespace Microsoft.NET.ToolPack.Tests
{
    public class GivenThatWeWantToPackAToolProject : SdkTest
    {
        public GivenThatWeWantToPackAToolProject(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_packs_successfully()
        {
            var helloWorldAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", "PackHelloWorld")
                .WithSource()
                .Restore(Log);

            var packCommand = new PackCommand(Log, helloWorldAsset.TestRoot);

            packCommand
                .Execute()
                .Should()
                .Pass();


            string nugetPackage = packCommand.GetNuGetPackage();
            this.Log.WriteLine(nugetPackage);
            //nugetPackage.Should().Be("aasdasd");
            //  File.Exists(nugetPackage).Should().BeTrue();


            using (var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                var libItems = nupkgReader.GetLibItems().ToList();
                libItems.Should().BeEmpty();
            }
        }
    }
}
