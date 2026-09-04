// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests
{
    /// <summary>
    /// The <c>dotnet watch</c> browser tools client and its configuration are served by the
    /// application, never by the provider that the browser authenticates. These tests pin the
    /// build integration that makes that possible: the configuration module is generated into the
    /// intermediate output with the public half of the invocation scoped key, it is a build only
    /// asset, and it never reaches publish output or a plain build.
    /// </summary>
    [TestClass]
    public class DotNetWatchBrowserToolsIntegrationTest : IsolatedNuGetPackageFolderAspNetSdkBaselineTest
    {
        protected override string RestoreNugetPackagePath => nameof(DotNetWatchBrowserToolsIntegrationTest);

        private const string TestAsset = "RazorComponentApp";
        private const string ConfigFileName = "Microsoft.NET.Sdk.Web.DotNetWatch.BrowserTools.Config.js";
        private const string ClientFileName = "Microsoft.NET.Sdk.Web.DotNetWatch.BrowserTools.js";

        // A syntactically valid base64 SubjectPublicKeyInfo stand-in. The build only substitutes it.
        private const string PublicKey = "TUlJQkltFrstKey0000";
        private const string OtherPublicKey = "TUlJQkltSecondKey111";

        private string GeneratedDirectory(MSBuildCommand command)
            => Path.Combine(command.GetIntermediateDirectory(DefaultTfm, "Debug").ToString(), "dotnet-watch");

        private static string[] WatchArguments(string publicKey)
            => ["/p:DotNetWatchBrowserTools=true", $"/p:DotNetWatchBrowserToolsPublicKey={publicKey}"];

        [TestMethod]
        public void Build_GeneratesConfigurationModuleWithThePinnedPublicKey()
        {
            var projectDirectory = CreateAspNetSdkTestAsset(TestAsset);
            var build = CreateBuildCommand(projectDirectory);

            ExecuteCommand(build, WatchArguments(PublicKey)).Should().Pass();

            var generated = GeneratedDirectory(build);
            var config = new FileInfo(Path.Combine(generated, ConfigFileName));
            config.Should().Exist();

            var content = File.ReadAllText(config.FullName);
            content.Should().Contain(PublicKey);
            content.Should().Contain("/_framework/dotnet-browser-tools/connect");
            content.Should().Contain("/_framework/dotnet-browser-tools/clear-cache");

            // The configuration only carries data; the executable client is a separate module that
            // it imports from the application's own origin.
            content.Should().Contain($"./{ClientFileName}");
            new FileInfo(Path.Combine(generated, ClientFileName)).Should().Exist();
        }

        /// <summary>
        /// The private key never leaves the <c>dotnet watch</c> process, so nothing but the public
        /// half may appear anywhere in the build output.
        /// </summary>
        [TestMethod]
        public void Build_RegistersAssetsAsBuildOnlyAndKeepsThemOutOfTheOutputDirectory()
        {
            var projectDirectory = CreateAspNetSdkTestAsset(TestAsset);
            var build = CreateBuildCommand(projectDirectory);

            ExecuteCommand(build, WatchArguments(PublicKey)).Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(
                File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json")));

            var assets = manifest.Assets.Where(a => a.RelativePath.Contains("DotNetWatch.BrowserTools")).ToArray();

            // The client and its configuration, plus the compressed alternative of each.
            assets.Where(a => a.IsPrimaryAsset()).Should().HaveCount(2);
            assets.Should().OnlyContain(a => a.IsBuildOnly());

            Directory.GetFiles(
                build.GetOutputDirectory(DefaultTfm, "Debug").ToString(),
                "*BrowserTools*",
                SearchOption.AllDirectories).Should().BeEmpty();
        }

        /// <summary>
        /// The modules are imported by name from the application, so both the fingerprinted and the
        /// plain route have to resolve.
        /// </summary>
        [TestMethod]
        public void Build_DefinesEndpointsForTheGeneratedModules()
        {
            var projectDirectory = CreateAspNetSdkTestAsset(TestAsset);
            var build = CreateBuildCommand(projectDirectory);

            ExecuteCommand(build, WatchArguments(PublicKey)).Should().Pass();

            var endpoints = File.ReadAllText(Path.Combine(
                build.GetOutputDirectory(DefaultTfm, "Debug").ToString(),
                "ComponentApp.staticwebassets.endpoints.json"));

            endpoints.Should().Contain($"_framework/{ConfigFileName}");
            endpoints.Should().Contain($"_framework/{ClientFileName}");
        }

        [TestMethod]
        public void Build_WithoutTheWatchProperty_DoesNotGenerateAnything()
        {
            var projectDirectory = CreateAspNetSdkTestAsset(TestAsset);
            var build = CreateBuildCommand(projectDirectory);

            ExecuteCommand(build).Should().Pass();

            new DirectoryInfo(GeneratedDirectory(build)).Should().NotExist();
        }

        /// <summary>
        /// Browser refresh can be suppressed, in which case there is no key and nothing may be
        /// generated or activated.
        /// </summary>
        [TestMethod]
        public void Build_WithEmptyPublicKey_DoesNotGenerateAnything()
        {
            var projectDirectory = CreateAspNetSdkTestAsset(TestAsset);
            var build = CreateBuildCommand(projectDirectory);

            ExecuteCommand(build, "/p:DotNetWatchBrowserTools=true", "/p:DotNetWatchBrowserToolsPublicKey=").Should().Pass();

            new DirectoryInfo(GeneratedDirectory(build)).Should().NotExist();
        }

        /// <summary>
        /// The key is stable for the whole watch invocation, so an incremental rebuild must not
        /// rewrite the generated files: rewriting them would invalidate downstream incrementality.
        /// A new invocation rotates the key and the content has to follow.
        /// </summary>
        [TestMethod]
        public void Rebuild_IsIncrementalForTheSameKeyAndRotatesForANewOne()
        {
            var projectDirectory = CreateAspNetSdkTestAsset(TestAsset);
            var build = CreateBuildCommand(projectDirectory);

            ExecuteCommand(build, WatchArguments(PublicKey)).Should().Pass();

            var configPath = Path.Combine(GeneratedDirectory(build), ConfigFileName);
            var thumbprint = FileThumbPrint.Create(configPath);

            ExecuteCommand(CreateBuildCommand(projectDirectory), WatchArguments(PublicKey)).Should().Pass();
            Assert.AreEqual(thumbprint, FileThumbPrint.Create(configPath));

            ExecuteCommand(CreateBuildCommand(projectDirectory), WatchArguments(OtherPublicKey)).Should().Pass();
            Assert.AreNotEqual(thumbprint, FileThumbPrint.Create(configPath));
            File.ReadAllText(configPath).Should().Contain(OtherPublicKey).And.NotContain(PublicKey);
        }

        [TestMethod]
        public void Publish_NeverContainsSessionAssets()
        {
            var projectDirectory = CreateAspNetSdkTestAsset(TestAsset);
            var publish = CreatePublishCommand(projectDirectory);

            ExecuteCommand(publish, WatchArguments(PublicKey)).Should().Pass();

            var publishManifest = File.ReadAllText(Path.Combine(
                publish.GetIntermediateDirectory(DefaultTfm, "Debug").ToString(),
                "staticwebassets.publish.json"));

            publishManifest.Should().NotContain("DotNetWatch.BrowserTools");
            publishManifest.Should().NotContain(PublicKey);

            Directory.GetFiles(
                publish.GetOutputDirectory(DefaultTfm, "Debug").ToString(),
                "*BrowserTools*",
                SearchOption.AllDirectories).Should().BeEmpty();
        }

        /// <summary>
        /// The generated files are tracked so that a clean removes them and no session material is
        /// left behind on disk.
        /// </summary>
        [TestMethod]
        public void Clean_RemovesTheGeneratedAssets()
        {
            var projectDirectory = CreateAspNetSdkTestAsset(TestAsset);
            var build = CreateBuildCommand(projectDirectory);

            ExecuteCommand(build, WatchArguments(PublicKey)).Should().Pass();
            new FileInfo(Path.Combine(GeneratedDirectory(build), ConfigFileName)).Should().Exist();

            var clean = new MSBuildCommand(Log, "Clean", build.FullPathProjectFile);
            ExecuteCommand(clean).Should().Pass();

            new FileInfo(Path.Combine(GeneratedDirectory(build), ConfigFileName)).Should().NotExist();
        }
    }
}
