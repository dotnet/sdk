﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
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

namespace Microsoft.NET.Pack.Tests
{
    public class GivenThatWeWantToPackAHelloWorldProject : SdkTest
    {
        public GivenThatWeWantToPackAHelloWorldProject(ITestOutputHelper log) : base(log)
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

            //  Validate the contents of the NuGet package by looking at the generated .nuspec file, as that's simpler
            //  than unzipping and inspecting the .nupkg
            string nuspecPath = packCommand.GetIntermediateNuspecPath();
            var nuspec = XDocument.Load(nuspecPath);

            var ns = nuspec.Root.Name.Namespace;
            XElement filesSection = nuspec.Root.Element(ns + "files");

            var fileTargets = filesSection.Elements().Select(files => files.Attribute("target").Value).ToList();

            var expectedFileTargets = new[]
            {
                @"lib\netcoreapp1.1\HelloWorld.runtimeconfig.json",
                @"lib\netcoreapp1.1\HelloWorld.dll"
            }.Select(p => p.Replace('\\', Path.DirectorySeparatorChar));

            fileTargets.Should().BeEquivalentTo(expectedFileTargets);
        }
    }
}
