// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Utilities;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class StaticWebAssets2IntegrationTest : AspNetSdkTest
    {
        public StaticWebAssets2IntegrationTest(ITestOutputHelper log) : base(log) {}

        // Build
        [Fact]
        public void Build_GeneratesJsonManifestAndCopiesItToOutputFolder()
        {
            var testAsset = "RazorComponentApp";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new BuildCommand(projectDirectory);
            build.Execute("/bl").Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should generate the manifest and the cache.
            var path = Path.Combine(outputPath, "RazorComponentApp.dll");
            new FileInfo(path).Should().Exist();
        }

        // Standalone project

        // Project with class library

        // Project with transitive class library

        // Project with "package reference"

        // Project with "transitive package reference"

        // Build no dependencies

        // Rebuild

        // Publish

        // Pack

        // Clean
    }
}
