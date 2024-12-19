// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    public class VanillaWasmTests(ITestOutputHelper log) : BlazorWasmBaselineTests(log, GenerateBaselines)
    {
        [CoreMSBuildOnlyFact]
        public void Build_Works()
        {
            var testAsset = "VanillaWasm";
            var targetFramework = "net9.0";
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
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.boot.json")).Should().Exist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "blazor.webassembly.js")).Should().NotExist();
            new FileInfo(Path.Combine(buildOutputDirectory, "wwwroot", "_framework", "dotnet.native.wasm")).Should().Exist();
        }
    }
}
