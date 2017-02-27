// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;
using System.Linq;
using FluentAssertions;
using System.Xml.Linq;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System;
using System.Runtime.CompilerServices;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildALibraryWithTfm : SdkTest
    {


        public static IEnumerable<object[]> GetTestData()
        {
            return new List<object[]>
            {
                new object[] 
                {
                    "monoandroid", "", false, false, false, false
                },
                new object[]
                {
                    "net40-client", null, true, false, false, false
                },
                new object[]
                {
                    "net45", null, true, false, false, false
                },
                new object[]
                {
                    "netstandard1.5", null, false, false, true, false
                },
                new object[]
                {
                    "portable-win81+wpa81", "", true, false, false, true
                },
                new object[]
                {
                    "portable-net451+wpa81+win81", "", true, false, false, false
                },
                new object[]
                {
                    "portable-net45+win8+wp8+wpa81", "", true, false, false, false
                },
                new object[]
                {
                    "sl5", "", true, false, false, false
                },
                new object[]
                {
                    "win8", "", true, false, false, true
                },
                new object[]
                {
                    "win81", "", true, false, false, true
                },
                new object[]
                {
                    "wp8", "", true, false, false, false
                },
                new object[]
                {
                    "wp81", "", true, false, false, false
                },
                new object[]
                {
                    "wpa81", "", true, false, false, true
                },
                new object[]
                {
                    "uap10.0", "", false, false, false, true
                },
                new object[]
                {
                    "xamarinios", "", false, true, false, false
                },
                new object[]
                {
                    "xamarinmac", "", false, false, false, false
                },
                new object[]
                {
                    "xamarintvos", "", false, true, false, false
                },
                new object[]
                {
                    "xamarinwatchos", "", false, true, false, false
                },
            };
        }

        [Theory]
        [MemberData(nameof(GetTestData))]
        public void It_builds_the_tfm_library_successfully_on_windows(string tfm, string langTargets, 
                                                                      bool hasLocalRef, bool hasMdb, bool hasDeps, bool hasPri)
        {
            if (!UsingFullFrameworkMSBuild)
            {
                return;
            }

            // TODO: Check existence of targets

            var testAsset = _testAssetsManager
                .CopyTestAsset("LibraryWithTfm")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propGroup = project.Root.Element(ns + "PropertyGroup");
                    var targetFramework = propGroup.Element(ns + "TargetFramework");
                    // Set the TFM
                    targetFramework.Value = tfm;
                })
                .Restore("TheLibrary");

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "TheLibrary");

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(tfm);

            var filesToCheck = new List<string>
            {
                "TheLibrary.dll",
                "TheLibrary.pdb"
            };

            if (hasDeps)
                filesToCheck.Add("TheLibrary.deps.json");
            if (hasMdb)
                filesToCheck.Add("TheLibrary.dll.mdb");
            if (hasPri)
                filesToCheck.Add("TheLibrary.pri");
            if (hasLocalRef)
                filesToCheck.Add("Newtonsoft.Json.dll");

            outputDirectory.Should().HaveFiles(filesToCheck);
        }
    }
}