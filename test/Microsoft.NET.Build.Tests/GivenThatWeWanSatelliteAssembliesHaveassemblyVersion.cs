// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;
using Microsoft.DotNet.Cli.Utils;
using System.Xml.Linq;
using System.Runtime.CompilerServices;
using Xunit.Abstractions;
using System.Diagnostics;
using FluentAssertions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWanSatelliteAssembliesHaveassemblyVersion: SdkTest
    {
        public GivenThatWeWanSatelliteAssembliesHaveassemblyVersion(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_should_produce_same_SatelliteAssemblie_versions_as_main()
        {
            if (UsingFullFrameworkMSBuild)
            {
                //  Disable this test on full framework, as generating strong named satellite assemblies with AL.exe requires Admin permissions
                //  See https://github.com/dotnet/sdk/issues/732
                return;
            }

            var testAsset = _testAssetsManager
              .CopyTestAsset("AllResourcesInSatelliteDisableVersionGenerate")
              .WithSource();

            testAsset = testAsset.Restore(Log);

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory("netcoreapp1.1");
            var file = Path.Combine(outputDirectory.FullName, "AllResourcesInSatellite.dll");

            var file2 = Path.Combine(outputDirectory.FullName, "en", "AllResourcesInSatellite.resources.dll");

            var versioninfo = FileVersionInfo.GetVersionInfo(file);
            var versioninfo2 = FileVersionInfo.GetVersionInfo(file2);
            versioninfo2.FileVersion.Should().Be(versioninfo.FileVersion);

        }
    }
}
