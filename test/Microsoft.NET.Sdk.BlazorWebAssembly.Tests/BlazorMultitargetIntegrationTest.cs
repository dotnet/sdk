// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Sdk.Razor.Tests;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    public class BlazorMultitargetIntegrationTest(ITestOutputHelper log)
        : IsolatedNuGetPackageFolderAspNetSdkBaselineTest(log, nameof(BlazorMultitargetIntegrationTest))
    {

        [RequiresMSBuildVersionFact("17.12", Reason = "Needs System.Text.Json 8.0.5")]
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

        [RequiresMSBuildVersionFact("17.12", Reason = "Needs System.Text.Json 8.0.5")]
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
