// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Utilities;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class ScopedCssIntegrationTest : SdkTest
    {
        public ScopedCssIntegrationTest(ITestOutputHelper log) : base(log) {}

        [Fact]
        public void Build_NoOps_WhenScopedCssIsDisabled()
        {
            var testAsset = "ComponentApp";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory);
            build.Execute("/p:ScopedCssEnabled=false").Should().Pass();

            var intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", "net5.0");

            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "Components", "Pages", "Counter.razor.rz.scp.css")).Should().NotExist();
            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "Components", "Pages", "Index.razor.rz.scp.css")).Should().NotExist();
            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "bundle", "ComponentApp.styles.css")).Should().NotExist();
            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "Components", "Pages", "FetchData.razor.rz.scp.css")).Should().NotExist();
        }

        [Fact]
        public void CanDisableDefaultDiscoveryConvention()
        {
            var testAsset = "ComponentApp";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory);
            build.Execute("/p:EnableDefaultScopedCssItems=false").Should().Pass();

            var intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", "net5.0");

            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "Components", "Pages", "Counter.razor.rz.scp.css")).Should().NotExist();
            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "Components", "Pages", "Index.razor.rz.scp.css")).Should().NotExist();
            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "bundle", "ComponentApp.styles.css")).Should().NotExist();
            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "Components", "Pages", "FetchData.razor.rz.scp.css")).Should().NotExist();
        }

        [Fact]
        public void CanOverrideScopeIdentifiers()
        {
            var testAsset = "ComponentApp";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var itemGroup = new XElement(ns + "ItemGroup");
                    var element = new XElement("ScopedCssInput", new XAttribute("Include", @"Styles\Pages\Counter.css"));
                    element.Add(new XElement("RazorComponent", @"Components\Pages\Counter.razor"));
                    element.Add(new XElement("CssScope", "b-overriden"));
                    itemGroup.Add(element);
                    project.Root.Add(itemGroup);
                });

            var stylesFolder = Path.Combine(projectDirectory.Path, "Styles", "Pages");
            Directory.CreateDirectory(stylesFolder);
            var styles = Path.Combine(stylesFolder, "Counter.css");
            File.Move(Path.Combine(projectDirectory.Path, "Components", "Pages", "Counter.razor.css"), styles);

            var build = new BuildCommand(projectDirectory);
            build.Execute("/p:EnableDefaultScopedCssItems=false").Should().Pass();

            var intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", "net5.0");

            var scoped = Path.Combine(intermediateOutputPath, "scopedcss", "Styles", "Pages", "Counter.rz.scp.css");
            new FileInfo(scoped).Should().Exist();
            new FileInfo(scoped).Should().Contain("b-overriden");
            var generated = Path.Combine(intermediateOutputPath, "Razor", "Components", "Pages", "Counter.razor.g.cs");
            new FileInfo(generated).Should().Exist();
            new FileInfo(generated).Should().Contain("b-overriden");
            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "Components", "Pages", "Index.razor.rz.scp.css")).Should().NotExist();
        }

        [Fact]
        public void Build_GeneratesTransformedFilesAndBundle_ForComponentsWithScopedCss()
        {
            var testAsset = "ComponentApp";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory);
            build.Execute().Should().Pass();

            var intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", "net5.0");

            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "Components", "Pages", "Counter.razor.rz.scp.css")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "Components", "Pages", "Index.razor.rz.scp.css")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "bundle", "ComponentApp.styles.css")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "projectbundle", "ComponentApp.bundle.scp.css")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "Components", "Pages", "FetchData.razor.rz.scp.css")).Should().NotExist();
        }

        [Fact]
        public void Build_ScopedCssFiles_ContainsUniqueScopesPerFile()
        {
            var testAsset = "ComponentApp";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory);
            build.Execute().Should().Pass();

            var intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", "net5.0");

            var generatedCounter = Path.Combine(intermediateOutputPath, "scopedcss", "Components", "Pages", "Counter.razor.rz.scp.css");
            new FileInfo(generatedCounter).Should().Exist();
            var generatedIndex = Path.Combine(intermediateOutputPath, "scopedcss", "Components", "Pages", "Index.razor.rz.scp.css");
            new FileInfo(generatedIndex).Should().Exist();
            var counterContent = File.ReadAllText(generatedCounter);
            var indexContent = File.ReadAllText(generatedIndex);

            var counterScopeMatch = Regex.Match(counterContent, ".*button\\[(.*)\\].*", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            Assert.True(counterScopeMatch.Success, "Couldn't find a scope id in the generated Counter scoped css file.");
            var counterScopeId = counterScopeMatch.Groups[1].Captures[0].Value;

            var indexScopeMatch = Regex.Match(indexContent, ".*h1\\[(.*)\\].*", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            Assert.True(indexScopeMatch.Success, "Couldn't find a scope id in the generated Index scoped css file.");
            var indexScopeId = indexScopeMatch.Groups[1].Captures[0].Value;

            Assert.NotEqual(counterScopeId, indexScopeId);
        }

        [Fact]
        public void Publish_PublishesBundleToTheRightLocation()
        {
            var testAsset = "ComponentApp";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var publish = new PublishCommand(Log, projectDirectory.TestRoot);
            publish.Execute().Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "ComponentApp.styles.css")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "ComponentApp", "Components", "Pages", "Index.razor.rz.scp.css")).Should().NotExist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "ComponentApp", "Components", "Pages", "Counter.razor.rz.scp.css")).Should().NotExist();
        }

        [Fact]
        public void Publish_NoBuild_PublishesBundleToTheRightLocation()
        {
            var testAsset = "ComponentApp";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory);
            build.Execute().Should().Pass();

            var publish = new PublishCommand(Log, projectDirectory.TestRoot);
            publish.Execute("/p:NoBuild=true").Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "ComponentApp.styles.css")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "ComponentApp", "Components", "Pages", "Index.razor.rz.scp.css")).Should().NotExist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "ComponentApp", "Components", "Pages", "Counter.razor.rz.scp.css")).Should().NotExist();
        }

        [Fact]
        public void Publish_DoesNotPublishAnyFile_WhenThereAreNoScopedCssFiles()
        {
            var testAsset = "ComponentApp";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            File.Delete(Path.Combine(projectDirectory.Path, "Components", "Pages", "Counter.razor.css"));
            File.Delete(Path.Combine(projectDirectory.Path, "Components", "Pages", "Index.razor.css"));

            var publish = new PublishCommand(Log, projectDirectory.TestRoot);
            publish.Execute().Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "ComponentApp", "_framework", "scoped.styles.css")).Should().NotExist();
        }

        [Fact]
        public void Publish_Publishes_IndividualScopedCssFiles_WhenNoBundlingIsEnabled()
        {
            var testAsset = "ComponentApp";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var publish = new PublishCommand(Log, projectDirectory.TestRoot);
            publish.Execute("/p:DisableScopedCssBundling=true").Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "ComponentApp", "ComponentApp.styles.css")).Should().NotExist();

            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "ComponentApp", "Components", "Pages", "Index.razor.rz.scp.css")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "ComponentApp", "Components", "Pages", "Counter.razor.rz.scp.css")).Should().Exist();
        }


        [Fact]
        public void Build_GeneratedComponentContainsScope()
        {
            var testAsset = "ComponentApp";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory);
            build.Execute().Should().Pass();

            var intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", "net5.0");

            var generatedCounter = Path.Combine(intermediateOutputPath, "scopedcss", "Components", "Pages", "Counter.razor.rz.scp.css");
            new FileInfo(generatedCounter).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "Razor", "Components", "Pages", "Counter.razor.g.cs")).Should().Exist();

            var counterContent = File.ReadAllText(generatedCounter);

            var counterScopeMatch = Regex.Match(counterContent, ".*button\\[(.*)\\].*", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            Assert.True(counterScopeMatch.Success, "Couldn't find a scope id in the generated Counter scoped css file.");
            var counterScopeId = counterScopeMatch.Groups[1].Captures[0].Value;

            new FileInfo(Path.Combine(intermediateOutputPath, "Razor", "Components", "Pages", "Counter.razor.g.cs")).Should().Contain(counterScopeId);
        }

        [Fact]
        public void Build_RemovingScopedCssAndBuilding_UpdatesGeneratedCodeAndBundle()
        {
            var testAsset = "ComponentApp";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            var build = new BuildCommand(projectDirectory);
            build.Execute().Should().Pass();

            var intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", "net5.0");

            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "Components", "Pages", "Counter.razor.rz.scp.css")).Should().Exist();
            var generatedBundle = Path.Combine(intermediateOutputPath, "scopedcss", "bundle", "ComponentApp.styles.css");
            new FileInfo(generatedBundle).Should().Exist();
            var generatedProjectBundle = Path.Combine(intermediateOutputPath, "scopedcss", "projectbundle", "ComponentApp.bundle.scp.css");
            new FileInfo(generatedProjectBundle).Should().Exist();
            var generatedCounter = Path.Combine(intermediateOutputPath, "Razor", "Components", "Pages", "Counter.razor.g.cs");
            new FileInfo(generatedCounter).Should().Exist();

            var componentThumbprint = FileThumbPrint.Create(generatedCounter);
            var bundleThumbprint = FileThumbPrint.Create(generatedBundle);

            File.Delete(Path.Combine(projectDirectory.Path, "Components", "Pages", "Counter.razor.css"));

            build = new BuildCommand(projectDirectory);
            build.Execute().Should().Pass();

            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "Components", "Pages", "Counter.razor.rz.scp.css")).Should().NotExist();
            new FileInfo(generatedCounter).Should().Exist();

            var newComponentThumbprint = FileThumbPrint.Create(generatedCounter);
            var newBundleThumbprint = FileThumbPrint.Create(generatedBundle);

            Assert.NotEqual(componentThumbprint, newComponentThumbprint);
            Assert.NotEqual(bundleThumbprint, newBundleThumbprint);
        }

        [Fact]
        public void Does_Nothing_WhenThereAreNoScopedCssFiles()
        {
            var testAsset = "ComponentApp";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            File.Delete(Path.Combine(projectDirectory.Path, "Components", "Pages", "Counter.razor.css"));
            File.Delete(Path.Combine(projectDirectory.Path, "Components", "Pages", "Index.razor.css"));

            var build = new BuildCommand(projectDirectory);
            build.Execute().Should().Pass();

            var intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", "net5.0");

            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "Components", "Pages", "Counter.razor.rz.scp.css")).Should().NotExist();
            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "Components", "Pages", "Index.razor.rz.scp.css")).Should().NotExist();
            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "_framework", "scoped.styles.css")).Should().NotExist();
        }

        [Fact]
        public void Build_ScopedCssTransformation_AndBundling_IsIncremental()
        {
            // Arrange
            var thumbprintLookup = new Dictionary<string, FileThumbPrint>();

            var testAsset = "ComponentApp";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource();

            // Act & Assert 1
            var build = new BuildCommand(projectDirectory);
            build.Execute().Should().Pass();

            var intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", "net5.0");
            var directoryPath = Path.Combine(intermediateOutputPath, "scopedcss");

            var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var thumbprint = FileThumbPrint.Create(file);
                thumbprintLookup[file] = thumbprint;
            }

            // Act & Assert 2
            for (var i = 0; i < 2; i++)
            {
                // We want to make sure nothing changed between multiple incremental builds.
                using (var razorGenDirectoryLock = LockDirectory(Path.Combine(intermediateOutputPath, "Razor")))
                {
                    build = new BuildCommand(projectDirectory);
                    build.Execute().Should().Pass();
                }

                foreach (var file in files)
                {
                    var thumbprint = FileThumbPrint.Create(file);
                    Assert.Equal(thumbprintLookup[file], thumbprint);
                }
            }
        }

        private IDisposable LockDirectory(string directory)
        {
            var disposables = new List<IDisposable>();
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                disposables.Add(File.Open(file, FileMode.Open, FileAccess.Read, FileShare.None));
            }

            var disposable = new Mock<IDisposable>();
            disposable.Setup(d => d.Dispose())
                .Callback(() => disposables.ForEach(d => d.Dispose()));

            return disposable.Object;
        }
    }
}
