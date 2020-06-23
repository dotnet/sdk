// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.ProjectConstruction;
using System.IO;
using System;
using System.Xml.Linq;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildWithATargetPlatform : SdkTest
    {
        public GivenThatWeWantToBuildWithATargetPlatform(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("android", new[] { "Android,Version=v7.0" })]
        [InlineData("macos", new[] { "macOS,Version=v8.0", "macOS,Version=v9.0", "macOS,Version=v10.0" })]
        [InlineData("ios", new[] { "Windows,Version=v7.0", "macOS,Version=v8.0", "Android,Version=v1.0", "iOS,Version=v1.0" })]
        public void It_passes_on_supported_os(string targetPlatform, string[] supportedTargetPlatform)
        {
            TestProject testProject = new TestProject()
            {
                Name = "SupportedOS",
                IsSdkProject = true,
                TargetFrameworks = "net5.0"
            };
            testProject.AdditionalProperties["_InferredTargetPlatform"] = "true";
            testProject.AdditionalProperties["TargetPlatformIdentifier"] = targetPlatform;
            var testAsset = _testAssetsManager.CreateTestProject(testProject).WithProjectChanges(project =>
            {
                //  Manually set SupportedTargetPlatform
                var ns = project.Root.Name.Namespace;

                var itemGroup = new XElement(ns + "ItemGroup");
                project.Root.Add(itemGroup);
                foreach (var platform in supportedTargetPlatform)
                {
                    itemGroup.Add(new XElement(ns + "SupportedTargetPlatform", new XAttribute("Include", platform)));
                }
            });

            var build = new BuildCommand(Log, Path.Combine(testAsset.Path, testProject.Name));
            build.Execute()
                .Should()
                .Pass();
        }

        [Theory]
        [InlineData("unsupported", new string[] { })]
        [InlineData("custom", new[] { "Windows,Version=v7.0", "macOS,Version=v8.0", "Android,Version=v1.0", "iOS,Version=v1.0" })]
        public void It_fails_on_unsupported_os(string targetPlatform, string[] supportedTargetPlatform)
        {
            TestProject testProject = new TestProject()
            {
                Name = "UnsupportedOS",
                IsSdkProject = true,
                TargetFrameworks = "net5.0"
            };
            testProject.AdditionalProperties["_InferredTargetPlatform"] = "true";
            testProject.AdditionalProperties["TargetPlatformIdentifier"] = targetPlatform;

            var testAsset = _testAssetsManager.CreateTestProject(testProject).WithProjectChanges(project =>
            {
                //  Manually set SupportedTargetPlatform
                var ns = project.Root.Name.Namespace;

                var itemGroup = new XElement(ns + "ItemGroup");
                project.Root.Add(itemGroup);
                foreach (var platform in supportedTargetPlatform)
                {
                    itemGroup.Add(new XElement(ns + "SupportedTargetPlatform", new XAttribute("Include", platform)));
                }
            });

            var build = new BuildCommand(Log, Path.Combine(testAsset.Path, testProject.Name));
            build.Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1134");
        }
    }
}
