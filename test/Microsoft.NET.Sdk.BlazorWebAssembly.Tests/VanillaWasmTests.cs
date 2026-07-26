// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections.Generic;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    [TestClass]
    public class VanillaWasmTests : BlazorWasmBaselineTests
    {
        [TestMethod]
        [CoreMSBuildOnly]
        public void Build_Works()
        {
            var testAsset = "VanillaWasm";
            var targetFramework = ToolsetInfo.CurrentTargetFramework;
            var testInstance = CreateAspNetSdkTestAsset(testAsset)
                .WithProjectChanges((p, doc) =>
                {
                    var itemGroup = new XElement("PropertyGroup");
                    var fingerprintAssets = new XElement("WasmFingerprintAssets", false);
                    itemGroup.Add(fingerprintAssets);
                    doc.Root.Add(itemGroup);
                });

            var build = CreateBuildCommand(testInstance);
            ExecuteCommand(build)
                .Should()
                .Pass();

            var buildOutputDirectory = Path.Combine(testInstance.Path, "bin", "Debug", targetFramework);

            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "dotnet.js")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", WasmBootConfigFileName)).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.webassembly.js")).Should().NotExist();
            // Framework assets are no longer copied to bin/_framework/ during build (dotnet/runtime#126407)
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "dotnet.native.wasm")).Should().NotExist();
        }

        [TestMethod]
        [CoreMSBuildOnly]
        [DataRow(null, "true", "true")]
        [DataRow("false", "false", "false")]
        public void Build_ResolvesBlazorDiagnosticsFeatureSwitches(string diagnosticsEnabled, string expectedDiagnosticsEnabled, string expectedFeatureValue)
        {
            var testInstance = CreateAspNetSdkTestAsset("BlazorWasmMinimal");
            var build = CreateBuildCommand(testInstance);

            var arguments = new List<string>
            {
                "-getProperty:BlazorWebAssemblyDiagnosticsEnabled",
                "-getProperty:MetricsSupport",
                "-getProperty:EventSourceSupport",
                "-getProperty:HttpActivityPropagationSupport"
            };

            if (diagnosticsEnabled is not null)
            {
                arguments.Add($"/p:BlazorWebAssemblyDiagnosticsEnabled={diagnosticsEnabled}");
            }

            var result = ExecuteCommand(build, arguments.ToArray());
            result.Should().Pass();

            using var propertiesDocument = JsonDocument.Parse(result.StdOut!);
            var properties = propertiesDocument.RootElement.GetProperty("Properties");

            properties.GetProperty("BlazorWebAssemblyDiagnosticsEnabled").GetString().Should().Be(expectedDiagnosticsEnabled);
            properties.GetProperty("MetricsSupport").GetString().Should().Be(expectedFeatureValue);
            properties.GetProperty("EventSourceSupport").GetString().Should().Be(expectedFeatureValue);
            properties.GetProperty("HttpActivityPropagationSupport").GetString().Should().Be(expectedFeatureValue);
        }
    }
}