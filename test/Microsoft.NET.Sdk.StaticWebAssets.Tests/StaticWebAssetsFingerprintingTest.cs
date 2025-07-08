// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using System.Text.Json;
using System.IO.Compression;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests;

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

    public static TheoryData<string, string, string, bool, bool> OverrideHtmlAssetPlaceholdersData => new TheoryData<string, string, string, bool, bool>
    {
        { "VanillaWasm", "main.js", "main#[.{fingerprint}].js", true, true },
        { "VanillaWasm", "main.js", null, false, false },
        { "BlazorWasmMinimal", "_framework/blazor.webassembly.js", "_framework/blazor.webassembly#[.{fingerprint}].js", false, true }
    };

    [Theory]
    [MemberData(nameof(OverrideHtmlAssetPlaceholdersData))]
    public void Build_OverrideHtmlAssetPlaceholders(string testAsset, string scriptPath, string scriptPathWithFingerprintPattern, bool fingerprintUserJavascriptAssets, bool expectFingerprintOnScript)
    {
        ProjectDirectory = CreateAspNetSdkTestAsset(testAsset, identifier: $"{testAsset}_{fingerprintUserJavascriptAssets}_{expectFingerprintOnScript}");
        ReplaceStringInIndexHtml(ProjectDirectory, scriptPath, scriptPathWithFingerprintPattern);
        FingerprintUserJavascriptAssets(fingerprintUserJavascriptAssets);

        var build = CreateBuildCommand(ProjectDirectory);
        ExecuteCommand(build, "-p:OverrideHtmlAssetPlaceholders=true", $"-p:FingerprintUserJavascriptAssets={fingerprintUserJavascriptAssets.ToString().ToLower()}").Should().Pass();

        var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
        var indexHtmlPath = Directory.EnumerateFiles(Path.Combine(intermediateOutputPath, "staticwebassets", "htmlassetplaceholders", "build"), "*.html").Single();
        var endpointsManifestPath = Path.Combine(intermediateOutputPath, $"staticwebassets.build.endpoints.json");

        AssertImportMapInHtml(indexHtmlPath, endpointsManifestPath, scriptPath, expectFingerprintOnScript: expectFingerprintOnScript, expectPreloadElement: testAsset == "VanillaWasm");
    }

    [Theory]
    [MemberData(nameof(OverrideHtmlAssetPlaceholdersData))]
    public void Publish_OverrideHtmlAssetPlaceholders(string testAsset, string scriptPath, string scriptPathWithFingerprintPattern, bool fingerprintUserJavascriptAssets, bool expectFingerprintOnScript)
    {
        ProjectDirectory = CreateAspNetSdkTestAsset(testAsset, identifier: $"{testAsset}_{fingerprintUserJavascriptAssets}_{expectFingerprintOnScript}");
        ReplaceStringInIndexHtml(ProjectDirectory, scriptPath, scriptPathWithFingerprintPattern);
        FingerprintUserJavascriptAssets(fingerprintUserJavascriptAssets);

        var projectName = Path.GetFileNameWithoutExtension(Directory.EnumerateFiles(ProjectDirectory.TestRoot, "*.csproj").Single());

        var publish = CreatePublishCommand(ProjectDirectory);
        ExecuteCommand(publish, "-p:OverrideHtmlAssetPlaceholders=true", $"-p:FingerprintUserJavascriptAssets={fingerprintUserJavascriptAssets.ToString().ToLower()}").Should().Pass();

        var outputPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();
        var indexHtmlOutputPath = Path.Combine(outputPath, "wwwroot", "index.html");
        var endpointsManifestPath = Path.Combine(outputPath, $"{projectName}.staticwebassets.endpoints.json");

        AssertImportMapInHtml(indexHtmlOutputPath, endpointsManifestPath, scriptPath, expectFingerprintOnScript: expectFingerprintOnScript, expectPreloadElement: testAsset == "VanillaWasm", assertHtmlCompressed: true);
    }

    private void FingerprintUserJavascriptAssets(bool fingerprintUserJavascriptAssets)
    {
        if (fingerprintUserJavascriptAssets)
        {
            ProjectDirectory.WithProjectChanges(p =>
            {
                if (p.Root != null)
                {
                    var itemGroup = new XElement("ItemGroup");
                    var pattern = new XElement("StaticWebAssetFingerprintPattern");
                    pattern.SetAttributeValue("Include", "Js");
                    pattern.SetAttributeValue("Pattern", "*.js");
                    pattern.SetAttributeValue("Expression", "#[.{fingerprint}]!");
                    itemGroup.Add(pattern);
                    p.Root.Add(itemGroup);
                }
            });
        }
    }

    private void ReplaceStringInIndexHtml(TestAsset testAsset, string sourceValue, string targetValue)
    {
        if (targetValue != null)
        {
            var indexHtmlPath = Path.Combine(testAsset.TestRoot, "wwwroot", "index.html");
            var indexHtmlContent = File.ReadAllText(indexHtmlPath);
            var newIndexHtmlContent = indexHtmlContent.Replace(sourceValue, targetValue);
            if (indexHtmlContent == newIndexHtmlContent)
                throw new Exception($"String replacement '{sourceValue}' for '{targetValue}' didn't produce any change in '{indexHtmlPath}'");

            File.WriteAllText(indexHtmlPath, newIndexHtmlContent);
        }
    }

    private void AssertImportMapInHtml(string indexHtmlPath, string endpointsManifestPath, string scriptPath, bool expectFingerprintOnScript = true, bool expectPreloadElement = false, bool assertHtmlCompressed = false)
    {

        var endpoints = JsonSerializer.Deserialize<StaticWebAssetEndpointsManifest>(File.ReadAllText(endpointsManifestPath));
        var fingerprintedScriptPath = GetFingerprintedPath(scriptPath);

        var indexHtmlContent = File.ReadAllText(indexHtmlPath);
        AssertHtmlContent(indexHtmlContent);

        if (assertHtmlCompressed)
        {
            var indexHtmlGzipContent = DecompressGzipFile(indexHtmlPath + ".gz");
            AssertHtmlContent(indexHtmlGzipContent);

            var indexHtmlBrotliContent = DecompressBrotliFile(indexHtmlPath + ".br");
            AssertHtmlContent(indexHtmlBrotliContent);
        }

        void AssertHtmlContent(string content)
        {
            if (expectFingerprintOnScript)
            {
                Assert.DoesNotContain($"src=\"{scriptPath}\"", content);
                Assert.Contains($"src=\"{fingerprintedScriptPath}\"", content);
            }
            else
            {
                Assert.Contains(scriptPath, content);

                if (scriptPath != fingerprintedScriptPath)
                {
                    Assert.DoesNotContain(fingerprintedScriptPath, content);
                }
            }

            Assert.Contains(GetFingerprintedPath("_framework/dotnet.js"), content);
            Assert.Contains(GetFingerprintedPath("_framework/dotnet.native.js"), content);
            Assert.Contains(GetFingerprintedPath("_framework/dotnet.runtime.js"), content);

            if (expectPreloadElement)
            {
                Assert.DoesNotContain("<link rel=\"preload\"", content);
                Assert.Contains($"<link href=\"{fingerprintedScriptPath}\" rel=\"preload\" as=\"script\" fetchpriority=\"high\" crossorigin=\"anonymous\"", content);
            }
        }

        string GetFingerprintedPath(string route)
            => endpoints.Endpoints.FirstOrDefault(e => e.Route == route && e.Selectors.Length == 0)?.AssetFile ?? throw new Exception($"Missing endpoint for file '{route}' in '{endpointsManifestPath}'");

        string DecompressGzipFile(string path)
        {
            if (File.Exists(path))
            {
                using var fileStream = File.OpenRead(path);
                using var compressedStream = new GZipStream(fileStream, CompressionMode.Decompress);
                using var reader = new StreamReader(compressedStream);
                return reader.ReadToEnd();
            }

            Assert.Fail($"File '{path}' does not exist.");
            return null;
        }

        string DecompressBrotliFile(string path)
        {
            if (File.Exists(path))
            {
                using var fileStream = File.OpenRead(path);
                using var compressedStream = new BrotliStream(fileStream, CompressionMode.Decompress);
                using var reader = new StreamReader(compressedStream);
                return reader.ReadToEnd();
            }

            Assert.Fail($"File '{path}' does not exist.");
            return null;
        }
    }
}
