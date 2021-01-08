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

namespace Microsoft.NET.Razor.Sdk.Tests
{
    public class BuildWithComponents31IntegrationTest : SdkTest
    {
        public BuildWithComponents31IntegrationTest(ITestOutputHelper log) : base(log) {}

        [Fact]
        public void Build_Components_WithDotNetCoreMSBuild_Works()
        {
            var testAsset = "blazor31";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory);
            build.Execute().Should().Pass();

            string outputPath = build.GetOutputDirectory("netcoreapp3.1").ToString();

            new FileInfo(Path.Combine(outputPath, "blazor31.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "blazor31.pdb")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "blazor31.Views.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "blazor31.Views.pdb")).Should().Exist();
        
            // Assert.AssemblyContainsType(result, Path.Combine(OutputPath, "blazor31.dll"), "blazor31.Pages.Index");
            // Assert.AssemblyContainsType(result, Path.Combine(OutputPath, "blazor31.dll"), "blazor31.Shared.NavMenu");

            // Verify a regular View appears in the views dll, but not in the main assembly.
            // Assert.AssemblyDoesNotContainType(result, Path.Combine(OutputPath, "blazor31.dll"), "blazor31.Pages.Pages__Host");
            // Assert.AssemblyContainsType(result, Path.Combine(OutputPath, "blazor31.Views.dll"), "blazor31.Pages.Pages__Host");
        }
    }
}
