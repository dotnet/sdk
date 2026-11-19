// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Sdk.WebAssembly;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests;

[TestClass]
public class BootJsonDataLoaderTests : SdkTest
{
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void ParseBootData_ReadsAppSettingsAssets(bool loadIntoVfs)
    {
        var bootConfigPath = Path.Combine(TestAssetsManager.CreateTestDirectory().Path, "dotnet.boot.js");
        File.WriteAllText(bootConfigPath, $$"""
            export const config = /*json-start*/{
              "appsettings": null,
              "resources": {
                "assembly": [{"name": "App.wasm", "hash": "sha256-app"}],
                "appsettings": [
                  {
                    "name": "../appsettings.development.json",
                    "virtualPath": "appsettings.development.json",
                    "loadIntoVfs": {{loadIntoVfs.ToString().ToLowerInvariant()}},
                    "cache": "no-cache"
                  },
                  {
                    "name": "../appsettings.json",
                    "virtualPath": "appsettings.json",
                    "loadIntoVfs": {{loadIntoVfs.ToString().ToLowerInvariant()}},
                    "cache": "no-cache",
                    "resolvedUrl": "./settings.json"
                  }
                ]
              }
            }/*json-end*/;
            """);

        var bootData = BootJsonDataLoader.ParseBootData(bootConfigPath);

        bootData.config.Should().Equal("../appsettings.development.json", "../appsettings.json");
        bootData.resources.assembly.Should().Contain("App.wasm", "sha256-app");
        bootData.resources.appsettings.Should().BeEquivalentTo(new[]
        {
            new AppSettingsAsset
            {
                name = "../appsettings.development.json",
                virtualPath = "appsettings.development.json",
                loadIntoVfs = loadIntoVfs,
                cache = "no-cache"
            },
            new AppSettingsAsset
            {
                name = "../appsettings.json",
                virtualPath = "appsettings.json",
                loadIntoVfs = loadIntoVfs,
                cache = "no-cache",
                resolvedUrl = "./settings.json"
            }
        });
    }

    [TestMethod]
    [DataRow("config")]
    [DataRow("appsettings")]
    public void ParseBootData_ReadsLegacySettings(string settingsProperty)
    {
        var bootConfigPath = Path.Combine(TestAssetsManager.CreateTestDirectory().Path, "blazor.boot.json");
        File.WriteAllText(bootConfigPath, $$"""
            {
              "{{settingsProperty}}": ["../appsettings.json", "../appsettings.development.json"],
              "resources": {
                "assembly": {"App.dll": "sha256-legacy"}
              }
            }
            """);

        var bootData = BootJsonDataLoader.ParseBootData(bootConfigPath);

        bootData.config.Should().Equal("../appsettings.json", "../appsettings.development.json");
        bootData.resources.assembly.Should().Contain("App.dll", "sha256-legacy");
        bootData.resources.appsettings.Should().BeNull();
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void ParseBootData_PreservesEmptyOrMissingSettings(bool includeEmptySettings)
    {
        var bootConfigPath = Path.Combine(TestAssetsManager.CreateTestDirectory().Path, "blazor.boot.json");
        File.WriteAllText(bootConfigPath, includeEmptySettings
            ? """{"resources": {"appsettings": []}}"""
            : """{"resources": {}}""");

        var bootData = BootJsonDataLoader.ParseBootData(bootConfigPath);

        if (includeEmptySettings)
        {
            bootData.config.Should().BeEmpty();
            bootData.resources.appsettings.Should().BeEmpty();
        }
        else
        {
            bootData.config.Should().BeNull();
            bootData.resources.appsettings.Should().BeNull();
        }
    }
}
