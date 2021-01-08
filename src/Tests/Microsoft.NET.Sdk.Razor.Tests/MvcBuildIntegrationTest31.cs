// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class MvcBuildIntegrationTest31 : MvcBuildIntegrationTestLegacy
    {
        public MvcBuildIntegrationTest31(ITestOutputHelper log) : base(log) {}

        public override string TestProjectName => "SimpleMvc31";
        public override string TargetFramework => "netcoreapp3.1";

        [Fact]
        public void Build_WithGenerateRazorHostingAssemblyInfo_AddsConfigurationMetadata()
        {
            var testAsset = TestProjectName;
            var project = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(project);
            build.Execute("/p:GenerateRazorHostingAssemblyInfo=true").Should().Pass();

            var intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", TargetFramework);

            var razorAssemblyInfo = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", TargetFramework, "SimpleMvc31.RazorAssemblyInfo.cs");

            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc31.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "SimpleMvc31.Views.pdb")).Should().Exist();

            new FileInfo(razorAssemblyInfo).Should().Exist();
            new FileInfo(razorAssemblyInfo).Should().Contain("[assembly: Microsoft.AspNetCore.Razor.Hosting.RazorLanguageVersionAttribute(\"3.0\")]");
            new FileInfo(razorAssemblyInfo).Should().Contain("[assembly: Microsoft.AspNetCore.Razor.Hosting.RazorConfigurationNameAttribute(\"MVC-3.0\")]");
            new FileInfo(razorAssemblyInfo).Should().Contain("[assembly: Microsoft.AspNetCore.Razor.Hosting.RazorExtensionAssemblyNameAttribute(\"MVC-3.0\", \"Microsoft.AspNetCore.Mvc.Razor.Extensions\")]");
        }
    }
}
