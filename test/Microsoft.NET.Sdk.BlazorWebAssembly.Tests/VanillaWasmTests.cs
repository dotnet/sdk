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
        [DataRow("false", null, true)]
        [DataRow("false", "true", true)]
        [DataRow("false", "false", false)]
        [DataRow("true", null, false)]
        [DataRow("true", "false", false)]
        public void Restore_DownloadsCrossgen2ForCoreClrBrowserDefault(string useMonoRuntime, string readyToRun, bool expectCrossgen2)
        {
            var testInstance = CreateAspNetSdkTestAsset("VanillaWasm", identifier: $"{useMonoRuntime}_{readyToRun ?? "default"}")
                .WithProjectChanges((project, doc) =>
                {
                    var properties = new XElement("PropertyGroup",
                        new XElement("UseMonoRuntime", useMonoRuntime),
                        // Native relinking independently requests crossgen2; isolate the ReadyToRun default.
                        new XElement("WasmBuildNative", "false"),
                        new XElement("PrunePackageTargetingPackRoots", "$(NetCoreTargetingPackRoot)"),
                        new XElement("NetCoreTargetingPackRoot", "$(MSBuildProjectDirectory)/empty-packs"));
                    if (readyToRun is not null)
                        properties.Add(new XElement("PublishReadyToRun", readyToRun));
                    doc.Root.Add(properties);
                });

            var restore = CreateRestoreCommand(testInstance);
            restore.WithEnvironmentVariable("DOTNETSDK_WORKLOAD_PACK_ROOTS", string.Empty);
            if (useMonoRuntime == "true")
            {
                ExecuteCommand(restore).Should().Fail().And.HaveStdOutContaining("NETSDK1242");
                return;
            }

            ExecuteCommand(restore).Should().Pass();

            using var assets = JsonDocument.Parse(File.ReadAllText(Path.Combine(testInstance.Path, "obj", "project.assets.json")));
            var downloadsCrossgen2 = assets.RootElement.GetProperty("project").GetProperty("frameworks").EnumerateObject()
                .Where(framework => framework.Value.TryGetProperty("downloadDependencies", out _))
                .SelectMany(framework => framework.Value.GetProperty("downloadDependencies").EnumerateArray())
                .Any(dependency => dependency.GetProperty("name").GetString().StartsWith("Microsoft.NETCore.App.Crossgen2.", StringComparison.Ordinal));
            downloadsCrossgen2.Should().Be(expectCrossgen2);
        }

        [TestMethod]
        [CoreMSBuildOnly]
        [DataRow("wasi-wasm", ToolsetInfo.CurrentTargetFramework)]
        [DataRow("browser-wasm", "net11.0")]
        public void Crossgen2PackDefault_DoesNotApplyToOtherTargets(string runtimeIdentifier, string targetFramework)
        {
            var testInstance = CreateAspNetSdkTestAsset("VanillaWasm", identifier: $"{runtimeIdentifier}_{targetFramework}")
                .WithProjectChanges((project, doc) => doc.Root.Add(
                    new XElement("PropertyGroup",
                        new XElement("UseMonoRuntime", "false"),
                        new XElement("RuntimeIdentifier", runtimeIdentifier),
                        new XElement("TargetFramework", targetFramework))));

            var command = new MSBuildCommand(testInstance, string.Empty);
            command.WithWorkingDirectory(testInstance.TestRoot);
            var result = ExecuteCommandWithoutRestore(command, "-getProperty:RequiresCrossgen2Pack");
            result.Should().Pass();
            result.StdOut.Trim().Should().BeEmpty();
        }

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