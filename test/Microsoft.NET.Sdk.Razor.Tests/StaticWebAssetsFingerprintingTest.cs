// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using System.Text.Json;

namespace Microsoft.NET.Sdk.Razor.Tests;

public class StaticWebAssetsContentFingerprintingIntegrationTest(ITestOutputHelper log) : AspNetSdkBaselineTest(log)
{
    [Fact]
    public void Build_FingerprintsContent_WhenEnabled()
    {
        var expectedManifest = LoadBuildManifest();
        var testAsset = "RazorComponentApp";
        ProjectDirectory = CreateAspNetSdkTestAsset(testAsset)
            .WithProjectChanges(p => {
                var fingerprintContent = p.Descendants()
                    .SingleOrDefault(e => e.Name.LocalName == "StaticWebAssetsFingerprintContent");
                fingerprintContent.Value = "true";
            });

        Directory.CreateDirectory(Path.Combine(ProjectDirectory.Path, "wwwroot", "css"));
        File.WriteAllText(Path.Combine(ProjectDirectory.Path, "wwwroot", "css", "fingerprint-site.css"), "body { color: red; }");

        var build = CreateBuildCommand(ProjectDirectory);
        ExecuteCommand(build).Should().Pass();

        var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
        var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

        // GenerateStaticWebAssetsManifest should generate the manifest file.
        var path = Path.Combine(intermediateOutputPath, "staticwebassets.build.json");
        new FileInfo(path).Should().Exist();
        var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(path));
        AssertManifest(manifest, expectedManifest);

        // GenerateStaticWebAssetsManifest should copy the file to the output folder.
        var finalPath = Path.Combine(outputPath, "ComponentApp.staticwebassets.runtime.json");
        new FileInfo(finalPath).Should().Exist();

        var manifest1 = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json")));
        AssertManifest(manifest1, expectedManifest);
        AssertBuildAssets(manifest1, outputPath, intermediateOutputPath);
    }

    public static TheoryData<string, string, string> WriteImportMapToHtmlData => new TheoryData<string, string, string>
    {
        { "VanillaWasm", "main.js", null },
        { "BlazorWasmMinimal", "_framework/blazor.webassembly.js", "_framework/blazor.webassembly#[.{fingerprint}].js" }
    };

    [Theory]
    [MemberData(nameof(WriteImportMapToHtmlData))]
    public void Build_WriteImportMapToHtml(string testAsset, string scriptPath, string scriptPathWithFingerprintPattern)
    {
        ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);
        ReplaceStringInIndexHtml(ProjectDirectory, scriptPath, scriptPathWithFingerprintPattern);

        var build = CreateBuildCommand(ProjectDirectory);
        ExecuteCommand(build, "-p:WriteImportMapToHtml=true").Should().Pass();

        var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
        var indexHtmlPath = Directory.EnumerateFiles(Path.Combine(intermediateOutputPath, "staticwebassets", "importmaphtml", "build"), "*.html").Single();
        var endpointsManifestPath = Path.Combine(intermediateOutputPath, $"staticwebassets.build.endpoints.json");

        AssertImportMapInHtml(indexHtmlPath, endpointsManifestPath, scriptPath);
    }

    [Theory]
    [MemberData(nameof(WriteImportMapToHtmlData))]
    public void Publish_WriteImportMapToHtml(string testAsset, string scriptPath, string scriptPathWithFingerprintPattern)
    {
        ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);
        ReplaceStringInIndexHtml(ProjectDirectory, scriptPath, scriptPathWithFingerprintPattern);

        var projectName = Path.GetFileNameWithoutExtension(Directory.EnumerateFiles(ProjectDirectory.TestRoot, "*.csproj").Single());

        var publish = CreatePublishCommand(ProjectDirectory);
        ExecuteCommand(publish, "-p:WriteImportMapToHtml=true").Should().Pass();

        var outputPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();
        var indexHtmlOutputPath = Path.Combine(outputPath, "wwwroot", "index.html");
        var endpointsManifestPath = Path.Combine(outputPath, $"{projectName}.staticwebassets.endpoints.json");

        AssertImportMapInHtml(indexHtmlOutputPath, endpointsManifestPath, scriptPath);
    }

    private void ReplaceStringInIndexHtml(TestAsset testAsset, string scriptPath, string scriptPathWithFingerprintPattern)
    {
        if (scriptPathWithFingerprintPattern != null)
        {
            var indexHtmlPath = Path.Combine(testAsset.TestRoot, "wwwroot", "index.html");
            var indexHtmlContent = File.ReadAllText(indexHtmlPath);
            var newIndexHtmlContent = indexHtmlContent.Replace(scriptPath, scriptPathWithFingerprintPattern);
            if (indexHtmlContent == newIndexHtmlContent)
                throw new Exception($"Script replacement '{scriptPath}' for '{scriptPathWithFingerprintPattern}' didn't produce any change in '{indexHtmlPath}'");

            File.WriteAllText(indexHtmlPath, newIndexHtmlContent);
        }
    }

    private void AssertImportMapInHtml(string indexHtmlPath, string endpointsManifestPath, string scriptPath)
    {
        var indexHtmlContent = File.ReadAllText(indexHtmlPath);
        var endpoints = JsonSerializer.Deserialize<StaticWebAssetEndpointsManifest>(File.ReadAllText(endpointsManifestPath));

        var fingerprintedScriptPath = GetFingerprintedPath(scriptPath);
        Assert.DoesNotContain($"src=\"{scriptPath}\"", indexHtmlContent);
        Assert.Contains($"src=\"{fingerprintedScriptPath}\"", indexHtmlContent);

        Assert.Contains(GetFingerprintedPath("_framework/dotnet.js"), indexHtmlContent);
        Assert.Contains(GetFingerprintedPath("_framework/dotnet.native.js"), indexHtmlContent);
        Assert.Contains(GetFingerprintedPath("_framework/dotnet.runtime.js"), indexHtmlContent);

        string GetFingerprintedPath(string route)
            => endpoints.Endpoints.FirstOrDefault(e => e.Route == route && e.Selectors.Length == 0)?.AssetFile ?? throw new Exception($"Missing endpoint for file '{route}' in '{endpointsManifestPath}'");
    }
}
