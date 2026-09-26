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
        public void Build_TestApplicationBuilds()
        {
            var testInstance = CreateAspNetSdkTestAsset("BlazorWasmTestApp");

            ExecuteCommand(CreateBuildCommand(testInstance))
                .Should()
                .Pass();

            var buildOutputDirectory = Path.Combine(testInstance.Path, "bin", "Debug", ToolsetInfo.CurrentTargetFramework);
            new FileInfo(Path.Combine(buildOutputDirectory, "BlazorWasmTestApp.dll")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "BlazorWasmTestApp.staticwebassets.endpoints.json")).Should().Exist();
        }

        [TestMethod]
        [CoreMSBuildOnly]
        [DataRow(null, false, "true", "true")]
        [DataRow("false", false, "false", "false")]
        // Setting the property from the project body must take effect (dotnet/sdk#55489): the defaults are
        // evaluated at Sdk.targets time so a value set in the .csproj is honored, not just a global property.
        [DataRow("false", true, "false", "false")]
        [DataRow("true", true, "true", "true")]
        public void Build_ResolvesBlazorDiagnosticsFeatureSwitches(string diagnosticsEnabled, bool setInProjectFile, string expectedDiagnosticsEnabled, string expectedFeatureValue)
        {
            var testInstance = CreateAspNetSdkTestAsset("BlazorWasmMinimal");

            if (setInProjectFile && diagnosticsEnabled is not null)
            {
                testInstance.WithProjectChanges((project, doc) =>
                {
                    var propertyGroup = new XElement("PropertyGroup");
                    propertyGroup.Add(new XElement("BlazorWebAssemblyDiagnosticsEnabled", diagnosticsEnabled));
                    doc.Root.Add(propertyGroup);
                });
            }

            var build = CreateBuildCommand(testInstance);

            var arguments = new List<string>
            {
                "-getProperty:BlazorWebAssemblyDiagnosticsEnabled",
                "-getProperty:MetricsSupport",
                "-getProperty:EventSourceSupport",
                "-getProperty:HttpActivityPropagationSupport"
            };

            if (!setInProjectFile && diagnosticsEnabled is not null)
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