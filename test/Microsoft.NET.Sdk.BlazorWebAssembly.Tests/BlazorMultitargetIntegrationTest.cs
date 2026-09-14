// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Sdk.StaticWebAssets.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    [TestClass]
    public class BlazorMultitargetIntegrationTest : IsolatedNuGetPackageFolderAspNetSdkBaselineTest
    {
        protected override string RestoreNugetPackagePath => nameof(BlazorMultitargetIntegrationTest);

        [TestMethod]
        [RequiresMSBuildVersion("17.12")]
        public void MultiTargetApp_LoadsTheCorrectSdkBasedOnTfm()
        {
            // Arrange
            var testAppName = "RazorComponentAppMultitarget";
            var testInstance = CreateMultitargetAspNetSdkTestAsset(testAppName);

            var buildCommand = CreateBuildCommand(testInstance);
            ExecuteCommand(buildCommand).Should().Pass();

            var serverDependencies = buildCommand.GetIntermediateDirectory(DefaultTfm);
            var browserDependencies = buildCommand.GetIntermediateDirectory($"{DefaultTfm}-browser1.0");

            serverDependencies.File("captured-references.txt").Should().NotContain("Microsoft.AspNetCore.Components.WebAssembly.dll");
            serverDependencies.File("captured-references.txt").Should().Contain("Microsoft.AspNetCore.Components.Server.dll");

            browserDependencies.File("captured-references.txt").Should().Contain("Microsoft.AspNetCore.Components.WebAssembly.dll");
            browserDependencies.File("captured-references.txt").Should().NotContain("Microsoft.AspNetCore.Components.Server.dll");
        }

        [TestMethod]
        [RequiresMSBuildVersion("17.12")]
        public void ReferencedMultiTargetApp_LoadsTheCorrectSdkBasedOnTfm()
        {
            // Arrange
            var testAppName = "RazorComponentAppMultitarget";
            var testInstance = CreateMultitargetAspNetSdkTestAsset(testAppName);

            var buildCommand = CreateBuildCommand(testInstance);
            ExecuteCommand(buildCommand).Should().Pass();

            var serverDependencies = buildCommand.GetIntermediateDirectory(DefaultTfm);
            var browserDependencies = buildCommand.GetIntermediateDirectory($"{DefaultTfm}-browser1.0");

            serverDependencies.File("captured-references.txt").Should().NotContain("Microsoft.AspNetCore.Components.WebAssembly.dll");
            serverDependencies.File("captured-references.txt").Should().Contain("Microsoft.AspNetCore.Components.Server.dll");

            browserDependencies.File("captured-references.txt").Should().Contain("Microsoft.AspNetCore.Components.WebAssembly.dll");
            browserDependencies.File("captured-references.txt").Should().NotContain("Microsoft.AspNetCore.Components.Server.dll");
        }
    }
}