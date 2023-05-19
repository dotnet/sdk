// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.NET.Sdk.BlazorWebAssembly.Tests.ServiceWorkerAssert;
using Microsoft.NET.Sdk.WebAssembly;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    public class BlazorMultitargetIntegrationTest : AspNetSdkTest
    {
        public BlazorMultitargetIntegrationTest(ITestOutputHelper log) : base(log) { }

        [Fact]
        public void MultiTargetApp_LoadsTheCorrectSdkBasedOnTfm()
        {
            // Arrange
            var testAppName = "RazorComponentAppMultitarget";
            var testInstance = CreateMultitargetAspNetSdkTestAsset(testAppName);

            var buildCommand = new BuildCommand(testInstance);
            buildCommand.WithWorkingDirectory(testInstance.Path);
            buildCommand.Execute("/bl").Should().Pass();

            var serverDependencies = buildCommand.GetIntermediateDirectory(DefaultTfm);
            var browserDependencies = buildCommand.GetIntermediateDirectory($"{DefaultTfm}-browser1.0");

            serverDependencies.File("captured-references.txt").Should().NotContain("Microsoft.AspNetCore.Components.WebAssembly.dll");
            serverDependencies.File("captured-references.txt").Should().Contain("Microsoft.AspNetCore.Components.Server.dll");

            browserDependencies.File("captured-references.txt").Should().Contain("Microsoft.AspNetCore.Components.WebAssembly.dll");
            browserDependencies.File("captured-references.txt").Should().NotContain("Microsoft.AspNetCore.Components.Server.dll");
        }
    }
}
