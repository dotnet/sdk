// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests;

[TestClass]
public class LocalStaticWebAssetsIntegrationTest : AspNetSdkTest
{
    [TestMethod]
    [DataRow("Default")]
    [DataRow("Root")]
    public void Build_LocalFilesAreNotDiscoveredByReferencingProject(string projectMode)
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
                        new XElement("StaticWebAssetBasePath", projectMode == "Default" ? "_content/RazorComponentLibrary" : "/")));
                    document.Root.Add(new XElement("ItemGroup",
                        new XElement("Content", new XAttribute("Update", "wwwroot/local.js"),
                            new XElement("AssetMode", "Local"))));
                }
            });

        var libraryDirectory = Path.Combine(projectDirectory.TestRoot, "RazorComponentLibrary");
        Directory.CreateDirectory(Path.Combine(libraryDirectory, "wwwroot"));
        File.WriteAllText(Path.Combine(libraryDirectory, "wwwroot", "local.js"), "local");
        var sharedPath = Path.Combine(libraryDirectory, "wwwroot", "shared.js");
        File.WriteAllText(sharedPath, "shared");
        File.WriteAllText(Path.Combine(projectDirectory.TestRoot, "RazorComponentApp", "Program.cs"), """
            using System;
            using System.IO;
            using System.Linq;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Hosting;
            using Microsoft.AspNetCore.Hosting.StaticWebAssets;

            var builder = WebApplication.CreateBuilder();
            builder.Configuration[WebHostDefaults.StaticWebAssetsKey] = args[0];
            StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
            var provider = builder.Environment.WebRootFileProvider;
            var prefix = args[1];
            var expectLocal = bool.Parse(args[2]);
            if (provider.GetFileInfo(prefix + "local.js").Exists != expectLocal ||
                provider.GetDirectoryContents(prefix).Any(file => file.Name == "local.js") != expectLocal)
            {
                throw new InvalidOperationException("Unexpected Local file discovery.");
            }
            using var reader = new StreamReader(provider.GetFileInfo(prefix + "shared.js").CreateReadStream());
            Console.WriteLine(reader.ReadToEnd());
            """);

        var build = CreateBuildCommand(projectDirectory, "RazorComponentApp");
        ExecuteCommand(build).Should().Pass();

        var application = Path.Combine(build.GetOutputDirectory(DefaultTfm, "Debug").ToString(), "ComponentApp.dll");
        var appManifest = Path.Combine(build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString(),
            "staticwebassets.development.json");
        var libraryBuild = CreateBuildCommand(projectDirectory, "RazorComponentLibrary");
        var libraryManifest = Path.Combine(libraryBuild.GetIntermediateDirectory(DefaultTfm, "Debug").ToString(),
            "staticwebassets.development.json");
        var prefix = projectMode == "Default" ? "/_content/RazorComponentLibrary/" : "/";

        new DotnetCommand(Log, application, appManifest, prefix, "false")
            .WithWorkingDirectory(Path.Combine(projectDirectory.TestRoot, "RazorComponentApp"))
            .Execute().Should().Pass().And.HaveStdOutContaining("shared");
        new DotnetCommand(Log, application, libraryManifest, "/", "true")
            .WithWorkingDirectory(Path.Combine(projectDirectory.TestRoot, "RazorComponentApp"))
            .Execute().Should().Pass().And.HaveStdOutContaining("shared");

        File.WriteAllText(sharedPath, "updated shared");
        new DotnetCommand(Log, application, appManifest, prefix, "false")
            .WithWorkingDirectory(Path.Combine(projectDirectory.TestRoot, "RazorComponentApp"))
            .Execute().Should().Pass().And.HaveStdOutContaining("updated shared");
    }

    [TestMethod]
    [DataRow("Default", false)]
    [DataRow("Root", false)]
    [DataRow("SelfContained", false)]
    [DataRow("Default", true)]
    [DataRow("Root", true)]
    [DataRow("SelfContained", true)]
    public void BuildAndPublish_LocalAssetsStayInTheirProject(string projectMode, bool precompressed)
    {
        var projectDirectory = CreateAspNetSdkTestAsset("RazorComponentAppWithReferenceMultitarget", identifier: $"{projectMode}_{precompressed}")
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
        if (precompressed)
        {
            var localPath = Path.Combine(libraryDirectory, "wwwroot", "local.js");
            using (var output = File.Create(localPath + ".gz"))
            using (var compressed = new GZipStream(output, CompressionMode.Compress))
            using (var input = File.OpenRead(localPath))
            {
                input.CopyTo(compressed);
            }
            using (var output = File.Create(localPath + ".br"))
            using (var compressed = new BrotliStream(output, CompressionMode.Compress))
            using (var input = File.OpenRead(localPath))
            {
                input.CopyTo(compressed);
            }
        }

        var build = CreateBuildCommand(projectDirectory, "RazorComponentApp");
        ExecuteCommand(build).Should().Pass();

        var appIntermediate = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
        var libraryBuild = CreateBuildCommand(projectDirectory, "RazorComponentLibrary");
        var libraryIntermediate = libraryBuild.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
        var libraryManifest = ReadManifest(libraryIntermediate, "build");
        var localAssets = libraryManifest.Assets.Where(a =>
            a.RelativePath.StartsWith("local", StringComparison.Ordinal)).ToArray();
        localAssets.Should().OnlyContain(a => a.AssetMode == StaticWebAsset.AssetModes.Local);
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
            "local*", SearchOption.AllDirectories).Should().BeEmpty();

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
