// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests;

[TestClass]
public class LocalStaticWebAssetsIntegrationTest : AspNetSdkTest
{
    [TestMethod]
    [DataRow("Default")]
    [DataRow("Root")]
    [DataRow("SelfContained")]
    public void BuildAndPublish_LocalAssetsStayInTheirProject(string projectMode)
    {
        var projectDirectory = CreateAspNetSdkTestAsset("RazorComponentAppWithReferenceMultitarget", identifier: projectMode)
            .WithProjectChanges((path, document) =>
            {
                if (Path.GetFileName(path) == "RazorComponentLibrary.csproj")
                {
                    document.Descendants("TargetFrameworks").Single()
                        .ReplaceWith(new XElement("TargetFramework", DefaultTfm));
                    document.Root.Add(new XElement("PropertyGroup",
                        new XElement("StaticWebAssetProjectMode", projectMode),
                        new XElement("StaticWebAssetBuildCompressAllAssets", "true")));
                    document.Root.Add(new XElement("ItemGroup",
                        new XElement("Content", new XAttribute("Update", "wwwroot/local.js"),
                            new XElement("AssetMode", "Local"))));
                }
            });

        var libraryDirectory = Path.Combine(projectDirectory.TestRoot, "RazorComponentLibrary");
        Directory.CreateDirectory(Path.Combine(libraryDirectory, "wwwroot"));
        File.WriteAllText(Path.Combine(libraryDirectory, "wwwroot", "local.js"),
            string.Concat(Enumerable.Repeat("console.log('local');\n", 100)));
        File.WriteAllText(Path.Combine(libraryDirectory, "wwwroot", "shared.js"), "console.log('shared');");

        var build = CreateBuildCommand(projectDirectory, "RazorComponentApp");
        ExecuteCommand(build).Should().Pass();

        var appIntermediate = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
        var libraryBuild = CreateBuildCommand(projectDirectory, "RazorComponentLibrary");
        var libraryIntermediate = libraryBuild.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
        var libraryManifest = ReadManifest(libraryIntermediate, "build");
        var localAssets = libraryManifest.Assets.Where(a => a.AssetMode == StaticWebAsset.AssetModes.Local &&
            a.RelativePath.StartsWith("local", StringComparison.Ordinal)).ToArray();
        localAssets.Should().ContainSingle(a => a.IsPrimaryAsset());
        localAssets.Should().Contain(a => a.IsAlternativeAsset() && a.AssetTraitValue == "gzip");
        AssertLocalEndpoints(libraryIntermediate, "build");
        AssertNotExported(ReadManifest(appIntermediate, "build"));

        var libraryPublish = CreatePublishCommand(projectDirectory, "RazorComponentLibrary");
        ExecuteCommand(libraryPublish).Should().Pass();
        ReadManifest(libraryIntermediate, "publish").Assets.Should()
            .Contain(a => a.AssetMode == StaticWebAsset.AssetModes.Local && a.RelativePath.StartsWith("local", StringComparison.Ordinal));
        AssertLocalEndpoints(libraryIntermediate, "publish");
        Directory.GetFiles(libraryPublish.GetOutputDirectory(DefaultTfm, "Debug").ToString(),
            "local*.js", SearchOption.AllDirectories).Should().ContainSingle();

        var appPublish = CreatePublishCommand(projectDirectory, "RazorComponentApp");
        ExecuteCommand(appPublish).Should().Pass();
        AssertNotExported(ReadManifest(appIntermediate, "publish"));
        Directory.GetFiles(appPublish.GetOutputDirectory(DefaultTfm, "Debug").ToString(),
            "local*.js", SearchOption.AllDirectories).Should().BeEmpty();

        void AssertNotExported(StaticWebAssetsManifest manifest)
        {
            manifest.Assets.Should().NotContain(a => a.SourceId == "RazorComponentLibrary" &&
                a.RelativePath.StartsWith("local", StringComparison.Ordinal));
            manifest.Endpoints.Should().NotContain(e => e.Route.Contains("local", StringComparison.Ordinal));
            if (projectMode != "SelfContained")
            {
                manifest.Assets.Should().Contain(a => a.SourceId == "RazorComponentLibrary" &&
                    a.RelativePath.StartsWith("shared", StringComparison.Ordinal));
            }
        }

        static StaticWebAssetsManifest ReadManifest(string intermediate, string kind) =>
            StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(
                Path.Combine(intermediate, $"staticwebassets.{kind}.json")));

        static void AssertLocalEndpoints(string intermediate, string kind)
        {
            var manifest = JsonSerializer.Deserialize(
                File.ReadAllBytes(Path.Combine(intermediate, $"staticwebassets.{kind}.endpoints.json")),
                StaticWebAssetsJsonSerializerContext.Default.StaticWebAssetEndpointsManifest);
            manifest.Endpoints.Should().Contain(e => e.Route == "local.js" && e.Selectors.Length == 0);
            manifest.Endpoints.Should().Contain(e => e.Route == "local.js" &&
                e.Selectors.Any(s => s.Name == "Content-Encoding" && s.Value == "gzip"));
        }
    }
}
