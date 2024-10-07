// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class ScopedCssIntegrationTest(ITestOutputHelper log)
        : IsolatedNuGetPackageFolderAspNetSdkBaselineTest(log, nameof(ScopedCssIntegrationTest))
    {
        [Fact]
        public void Build_NoOps_WhenScopedCssIsDisabled()
        {
            var testAsset = "RazorComponentApp";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = CreateBuildCommand(projectDirectory);
            ExecuteCommand(build, "/p:ScopedCssEnabled=false").Should().Pass();

            var intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", DefaultTfm);

            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "Components", "Pages", "Counter.razor.rz.scp.css")).Should().NotExist();
            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "Components", "Pages", "Index.razor.rz.scp.css")).Should().NotExist();
            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "bundle", "ComponentApp.styles.css")).Should().NotExist();
            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "Components", "Pages", "FetchData.razor.rz.scp.css")).Should().NotExist();
        }

        [Fact]
        public void Build_NoOps_ForMvcApp_WhenScopedCssIsDisabled()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = CreateBuildCommand(projectDirectory);
            ExecuteCommand(build, "/p:ScopedCssEnabled=false").Should().Pass();

            var intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", DefaultTfm);

            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "Views", "Home", "Index.cshtml.rz.scp.css")).Should().NotExist();
            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "Views", "Home", "Contact.cshtml.rz.scp.css")).Should().NotExist();
            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "bundle", "SimpleMvc.styles.css")).Should().NotExist();
            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "Views", "Home", "About.cshtml.rz.scp.css")).Should().NotExist();
        }

        [Fact]
        public void CanDisableDefaultDiscoveryConvention()
        {
            var testAsset = "RazorComponentApp";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = CreateBuildCommand(projectDirectory);
            ExecuteCommand(build, "/p:EnableDefaultScopedCssItems=false").Should().Pass();

            var intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", DefaultTfm);

            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "Components", "Pages", "Counter.razor.rz.scp.css")).Should().NotExist();
            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "Components", "Pages", "Index.razor.rz.scp.css")).Should().NotExist();
            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "bundle", "ComponentApp.styles.css")).Should().NotExist();
            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "Components", "Pages", "FetchData.razor.rz.scp.css")).Should().NotExist();
        }

        [CoreMSBuildOnlyFact]
        public void CanOverrideScopeIdentifiers()
        {
            var testAsset = "RazorComponentApp";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset)
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var itemGroup = new XElement(ns + "ItemGroup");
                    var element = new XElement("ScopedCssInput", new XAttribute("Include", @"Styles\Pages\Counter.css"));
                    element.Add(new XElement("RazorComponent", @"Components\Pages\Counter.razor"));
                    element.Add(new XElement("CssScope", "b-overridden"));
                    itemGroup.Add(element);
                    project.Root.Add(itemGroup);
                });

            var stylesFolder = Path.Combine(projectDirectory.Path, "Styles", "Pages");
            Directory.CreateDirectory(stylesFolder);
            var styles = Path.Combine(stylesFolder, "Counter.css");
            File.Move(Path.Combine(projectDirectory.Path, "Components", "Pages", "Counter.razor.css"), styles);

            var build = CreateBuildCommand(projectDirectory);
            ExecuteCommand(build, "/p:EnableDefaultScopedCssItems=false", "/p:EmitCompilerGeneratedFiles=true").Should().Pass();

            var intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", DefaultTfm);

            var scoped = Path.Combine(intermediateOutputPath, "scopedcss", "Styles", "Pages", "Counter.rz.scp.css");
            new FileInfo(scoped).Should().Exist();
            new FileInfo(scoped).Should().Contain("b-overridden");
            var generated = Path.Combine(intermediateOutputPath, "generated", "Microsoft.CodeAnalysis.Razor.Compiler", "Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator", "Components_Pages_Counter_razor.g.cs");
            new FileInfo(generated).Should().Exist();
            new FileInfo(generated).Should().Contain("b-overridden");
            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "Components", "Pages", "Index.razor.rz.scp.css")).Should().NotExist();
        }

        [Fact]
        public void Build_GeneratesTransformedFilesAndBundle_ForComponentsWithScopedCss()
        {
            var testAsset = "RazorComponentApp";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = CreateBuildCommand(projectDirectory);
            ExecuteCommand(build).Should().Pass();

            var intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", DefaultTfm);

            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "Components", "Pages", "Counter.razor.rz.scp.css")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "Components", "Pages", "Index.razor.rz.scp.css")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "bundle", "ComponentApp.styles.css")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "projectbundle", "ComponentApp.bundle.scp.css")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "Components", "Pages", "FetchData.razor.rz.scp.css")).Should().NotExist();
        }

        [Fact]
        public void Build_GeneratesTransformedFilesAndBundle_ForViewsWithScopedCss()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = CreateBuildCommand(projectDirectory);
            ExecuteCommand(build).Should().Pass();

            var intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", DefaultTfm);

            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "Views", "Home", "Index.cshtml.rz.scp.css")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "Views", "Home", "Contact.cshtml.rz.scp.css")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "bundle", "SimpleMvc.styles.css")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "projectbundle", "SimpleMvc.bundle.scp.css")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "Views", "Home", "About.cshtml.rz.scp.css")).Should().Exist();
        }

        [Fact]
        public void Build_ScopedCssFiles_ContainsUniqueScopesPerFile()
        {
            var testAsset = "RazorComponentApp";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = CreateBuildCommand(projectDirectory);
            ExecuteCommand(build).Should().Pass();

            var intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", DefaultTfm);

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
        public void Build_ScopedCssViews_ContainsUniqueScopesPerView()
        {
            var testAsset = "RazorSimpleMvc";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = CreateBuildCommand(projectDirectory);
            ExecuteCommand(build).Should().Pass();

            var intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", DefaultTfm);

            var generatedIndex = Path.Combine(intermediateOutputPath, "scopedcss", "Views", "Home", "Index.cshtml.rz.scp.css");
            new FileInfo(generatedIndex).Should().Exist();
            var generatedAbout = Path.Combine(intermediateOutputPath, "scopedcss", "Views", "Home", "About.cshtml.rz.scp.css");
            new FileInfo(generatedAbout).Should().Exist();
            var generatedContact = Path.Combine(intermediateOutputPath, "scopedcss", "Views", "Home", "Contact.cshtml.rz.scp.css");
            new FileInfo(generatedContact).Should().Exist();
            var indexContent = File.ReadAllText(generatedIndex);
            var aboutContent = File.ReadAllText(generatedAbout);
            var contactContent = File.ReadAllText(generatedContact);

            var indexScopeMatch = Regex.Match(indexContent, ".*p\\[(.*)\\].*", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            Assert.True(indexScopeMatch.Success, "Couldn't find a scope id in the generated Index scoped css file.");
            var indexScopeId = indexScopeMatch.Groups[1].Captures[0].Value;

            var aboutScopeMatch = Regex.Match(aboutContent, ".*h2\\[(.*)\\].*", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            Assert.True(aboutScopeMatch.Success, "Couldn't find a scope id in the generated About scoped css file.");
            var aboutScopeId = aboutScopeMatch.Groups[1].Captures[0].Value;

            var contactScopeMatch = Regex.Match(contactContent, ".*a\\[(.*)\\].*", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            Assert.True(contactScopeMatch.Success, "Couldn't find a scope id in the generated Contact scoped css file.");
            var contactScopeId = contactScopeMatch.Groups[1].Captures[0].Value;

            Assert.NotEqual(indexScopeId, aboutScopeId);
            Assert.NotEqual(indexScopeId, contactScopeId);
            Assert.NotEqual(aboutScopeId, contactScopeId);
        }

        [Fact]
        public void Build_WorksWhenViewsAndComponentsArePartOfTheSameProject_ContainsUniqueScopesPerFile()
        {
            var testAsset = "RazorMvcWithComponents";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = CreateBuildCommand(projectDirectory);
            ExecuteCommand(build).Should().Pass();

            var intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", DefaultTfm);

            var generatedIndex = Path.Combine(intermediateOutputPath, "scopedcss", "Views", "Home", "Index.cshtml.rz.scp.css");
            new FileInfo(generatedIndex).Should().Exist();

            var generatedCounter = Path.Combine(intermediateOutputPath, "scopedcss", "Components", "Counter.razor.rz.scp.css");
            new FileInfo(generatedCounter).Should().Exist();

            var indexContent = File.ReadAllText(generatedIndex);
            var counterContent = File.ReadAllText(generatedCounter);

            var indexScopeMatch = Regex.Match(indexContent, ".*p\\[(.*)\\].*", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            Assert.True(indexScopeMatch.Success, "Couldn't find a scope id in the generated Index scoped css file.");
            var indexScopeId = indexScopeMatch.Groups[1].Captures[0].Value;

            var counterScopeMatch = Regex.Match(counterContent, ".*div\\[(.*)\\].*", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            Assert.True(counterScopeMatch.Success, "Couldn't find a scope id in the generated Counter scoped css file.");
            var counterScopeId = counterScopeMatch.Groups[1].Captures[0].Value;

            Assert.NotEqual(indexScopeId, counterScopeId);
        }

        [Fact]
        public void Publish_PublishesScopedCssBundleToTheRightLocation()
        {
            var testAsset = "RazorComponentApp";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var publish = CreatePublishCommand(projectDirectory);
            ExecuteCommand(publish).Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "ComponentApp.styles.css")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "ComponentApp", "Components", "Pages", "Index.razor.rz.scp.css")).Should().NotExist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "ComponentApp", "Components", "Pages", "Counter.razor.rz.scp.css")).Should().NotExist();
        }

        [Fact]
        public void Publish_NoBuild_PublishesBundleToTheRightLocation()
        {
            var testAsset = "RazorComponentApp";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = CreateBuildCommand(projectDirectory);
            var buildResult = ExecuteCommand(build);
            buildResult.Should().Pass();

            var publish = CreatePublishCommand(projectDirectory);
            ExecuteCommand(publish, "/p:NoBuild=true").Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "ComponentApp.styles.css")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "ComponentApp", "Components", "Pages", "Index.razor.rz.scp.css")).Should().NotExist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "ComponentApp", "Components", "Pages", "Counter.razor.rz.scp.css")).Should().NotExist();
        }

        [Fact]
        public void Publish_DoesNotPublishAnyFile_WhenThereAreNoScopedCssFiles()
        {
            var testAsset = "RazorComponentApp";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            File.Delete(Path.Combine(projectDirectory.Path, "Components", "Pages", "Counter.razor.css"));
            File.Delete(Path.Combine(projectDirectory.Path, "Components", "Pages", "Index.razor.css"));

            var publish = CreatePublishCommand(projectDirectory);
            ExecuteCommand(publish).Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "ComponentApp", "_framework", "scoped.styles.css")).Should().NotExist();
        }

        [Fact]
        public void Publish_Publishes_IndividualScopedCssFiles_WhenNoBundlingIsEnabled()
        {
            var testAsset = "RazorComponentApp";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var publish = CreatePublishCommand(projectDirectory);
            ExecuteCommand(publish, "/p:DisableScopedCssBundling=true").Should().Pass();

            var publishOutputPath = publish.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "_content", "ComponentApp", "ComponentApp.styles.css")).Should().NotExist();

            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "Components", "Pages", "Index.razor.rz.scp.css")).Should().Exist();
            new FileInfo(Path.Combine(publishOutputPath, "wwwroot", "Components", "Pages", "Counter.razor.rz.scp.css")).Should().Exist();
        }

        [CoreMSBuildOnlyFact]
        public void Build_RemovingScopedCssAndBuilding_UpdatesGeneratedCodeAndBundle()
        {
            var testAsset = "RazorComponentApp";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = CreateBuildCommand(projectDirectory);
            ExecuteCommand(build, "/p:EmitCompilerGeneratedFiles=true").Should().Pass();

            var intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", DefaultTfm);

            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "Components", "Pages", "Counter.razor.rz.scp.css")).Should().Exist();
            var generatedBundle = Path.Combine(intermediateOutputPath, "scopedcss", "bundle", "ComponentApp.styles.css");
            new FileInfo(generatedBundle).Should().Exist();
            var generatedProjectBundle = Path.Combine(intermediateOutputPath, "scopedcss", "projectbundle", "ComponentApp.bundle.scp.css");
            new FileInfo(generatedProjectBundle).Should().Exist();
            var generatedCounter = Path.Combine(intermediateOutputPath, "generated", "Microsoft.CodeAnalysis.Razor.Compiler", "Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator", "Components_Pages_Counter_razor.g.cs");
            new FileInfo(generatedCounter).Should().Exist();

            var componentThumbprint = FileThumbPrint.Create(generatedCounter);
            var bundleThumbprint = FileThumbPrint.Create(generatedBundle);

            File.Delete(Path.Combine(projectDirectory.Path, "Components", "Pages", "Counter.razor.css"));

            build = CreateBuildCommand(projectDirectory);
            ExecuteCommand(build, "/p:EmitCompilerGeneratedFiles=true").Should().Pass();

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
            var testAsset = "RazorComponentApp";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            File.Delete(Path.Combine(projectDirectory.Path, "Components", "Pages", "Counter.razor.css"));
            File.Delete(Path.Combine(projectDirectory.Path, "Components", "Pages", "Index.razor.css"));

            var build = CreateBuildCommand(projectDirectory);
            ExecuteCommand(build).Should().Pass();

            var intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", DefaultTfm);

            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "Components", "Pages", "Counter.razor.rz.scp.css")).Should().NotExist();
            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "Components", "Pages", "Index.razor.rz.scp.css")).Should().NotExist();
            new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "_framework", "scoped.styles.css")).Should().NotExist();
        }

        [Fact]
        public void Build_ScopedCssTransformation_AndBundling_IsIncremental()
        {
            // Arrange
            var thumbprintLookup = new Dictionary<string, FileThumbPrint>();

            var testAsset = "RazorComponentApp";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            // Act & Assert 1
            var build = CreateBuildCommand(projectDirectory);
            ExecuteCommand(build).Should().Pass();

            var intermediateOutputPath = Path.Combine(build.GetBaseIntermediateDirectory().ToString(), "Debug", DefaultTfm);
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
                build = CreateBuildCommand(projectDirectory);
                ExecuteCommand(build).Should().Pass();

                foreach (var file in files)
                {
                    var thumbprint = FileThumbPrint.Create(file);
                    Assert.Equal(thumbprintLookup[file], thumbprint);
                }
            }
        }

        // This test verifies if the targets that VS calls to update scoped css works to update these files
        [Fact]
        public void RegeneratingScopedCss_ForProject()
        {
            // Arrange
            var testAsset = "RazorComponentApp";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = CreateBuildCommand(ProjectDirectory);
            ExecuteCommand(build).Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var bundlePath = Path.Combine(intermediateOutputPath, "scopedcss", "bundle", "ComponentApp.styles.css");

            new FileInfo(bundlePath).Should().Exist();

            // Make an edit
            var scopedCssFile = Path.Combine(ProjectDirectory.TestRoot, "Components", "Pages", "Index.razor.css");
            File.WriteAllLines(scopedCssFile, File.ReadAllLines(scopedCssFile).Concat(["body { background-color: orangered; }"]));

            build = CreateBuildCommand(ProjectDirectory);
            ExecuteCommand(build, "/t:UpdateStaticWebAssetsDesignTime").Should().Pass();

            // Verify the generated file contains newly added css
            AssertFileContains(bundlePath, "background-color: orangered");

            // Verify that CSS edits continue to apply after new JS modules are added to the project
            // https://github.com/dotnet/aspnetcore/issues/57599
            var collocatedJsFile = Path.Combine(ProjectDirectory.TestRoot, "Components", "Pages", "Index.razor.js");
            File.WriteAllLines(collocatedJsFile, ["console.log('Hello, world!');"]);
            File.WriteAllLines(scopedCssFile, File.ReadAllLines(scopedCssFile).Concat(["h1 { color: purple; }"]));

            build = CreateBuildCommand(ProjectDirectory);
            ExecuteCommand(build, "/t:UpdateStaticWebAssetsDesignTime").Should().Pass();

            // Verify the generated file contains newly added css
            AssertFileContains(bundlePath, "color: purple");

            static void AssertFileContains(string fileName, string content)
            {
                var fileInfo = new FileInfo(fileName);
                fileInfo.Should().Exist();
                fileInfo.ReadAllText().Should().Contain(content);
            }
        }
    }

    public class ScopedCssCompatibilityIntegrationTest(ITestOutputHelper log)
        : IsolatedNuGetPackageFolderAspNetSdkBaselineTest(log, Path.Combine(nameof(ScopedCssCompatibilityIntegrationTest), ".nuget"))
    {
        [Fact]
        public void ScopedCss_IsBackwardsCompatible_WithPreviousVersions()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset)
                .WithProjectChanges((project, document) =>
                {
                    if (Path.GetFileName(project) == "AnotherClassLib.csproj")
                    {
                        document.Descendants("TargetFramework").Single().ReplaceNodes("net5.0");
                    }
                    if (Path.GetFileName(project) == "ClassLibrary.csproj")
                    {
                        document.Descendants("TargetFramework").Single().ReplaceNodes("net5.0");
                    }
                });

            EnsureLocalPackagesExists();

            var restore = CreateRestoreCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            ExecuteCommand(restore).Should().Pass();

            var build = CreateBuildCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            ExecuteCommand(build).Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "AppWithPackageAndP2PReference.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().Exist();
            var manifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json")));
            AssertManifest(
                manifest,
                LoadBuildManifest());

            AssertBuildAssets(
                manifest,
                outputPath,
                intermediateOutputPath);

            var appBundle = new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "bundle", "AppWithPackageAndP2PReference.styles.css"));
            appBundle.Should().Exist();

            appBundle.Should().Contain("_content/ClassLibrary/ClassLibrary.bundle.scp.css");
            appBundle.Should().Match(""".*_content/RazorPackageLibraryDirectDependency/RazorPackageLibraryDirectDependency\.[a-zA-Z0-9]+\.bundle\.scp\.css.*""");
        }

        [Fact]
        public void ScopedCss_PublishIsBackwardsCompatible_WithPreviousVersions()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset)
                .WithProjectChanges((project, document) =>
                {
                    if (Path.GetFileName(project) == "AnotherClassLib.csproj")
                    {
                        document.Descendants("TargetFramework").Single().ReplaceNodes("net5.0");
                    }
                    if (Path.GetFileName(project) == "ClassLibrary.csproj")
                    {
                        document.Descendants("TargetFramework").Single().ReplaceNodes("net5.0");
                    }
                });

            EnsureLocalPackagesExists();

            var restore = CreateRestoreCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            ExecuteCommand(restore).Should().Pass();

            var build = CreatePublishCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            ExecuteCommand(build, "/bl").Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            var finalPath = Path.Combine(intermediateOutputPath, "staticwebassets.publish.json");
            new FileInfo(finalPath).Should().Exist();
            var publishManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.publish.json")));
            AssertManifest(
                publishManifest,
                LoadPublishManifest());

            AssertPublishAssets(
                publishManifest,
                outputPath,
                intermediateOutputPath);

            var appBundle = new FileInfo(Path.Combine(outputPath, "wwwroot", "AppWithPackageAndP2PReference.styles.css"));
            appBundle.Should().Exist();

            appBundle.Should().Contain("_content/ClassLibrary/ClassLibrary.bundle.scp.css");
            appBundle.Should().Match("""_content/RazorPackageLibraryDirectDependency/RazorPackageLibraryDirectDependency\.[a-zA-Z0-9]+\.bundle\.scp\.css""");
        }
    }

    public class ScopedCssPackageReferences(ITestOutputHelper log)
        : IsolatedNuGetPackageFolderAspNetSdkBaselineTest(log, Path.Combine(nameof(ScopedCssPackageReferences), ".nuget"))
    {
        [Fact]
        public void BuildProjectWithReferences_CorrectlyBundlesScopedCssFiles()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            EnsureLocalPackagesExists();

            var restore = CreateRestoreCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            ExecuteCommand(restore).Should().Pass();

            var build = CreateBuildCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            ExecuteCommand(build).Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            // GenerateStaticWebAssetsManifest should copy the file to the output folder.
            var finalPath = Path.Combine(outputPath, "AppWithPackageAndP2PReference.staticwebassets.runtime.json");
            new FileInfo(finalPath).Should().Exist();
            var buildManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(Path.Combine(intermediateOutputPath, "staticwebassets.build.json")));
            AssertManifest(
                buildManifest,
                LoadBuildManifest());

            AssertBuildAssets(
                buildManifest,
                outputPath,
                intermediateOutputPath);

            var appBundle = new FileInfo(Path.Combine(intermediateOutputPath, "scopedcss", "bundle", "AppWithPackageAndP2PReference.styles.css"));
            appBundle.Should().Exist();

            appBundle.Should().Match(""".*_content/RazorPackageLibraryDirectDependency/RazorPackageLibraryDirectDependency\.[a-zA-Z0-9]+\.bundle\.scp\.css.*""");
            appBundle.Should().Match(""".*_content/ClassLibrary/ClassLibrary\.[a-zA-Z0-9]+\.bundle\.scp\.css.*""");
        }

        // Regression test for https://github.com/dotnet/aspnetcore/issues/37592
        [Fact]
        public void RegeneratingScopedCss_ForProjectWithReferences()
        {
            var testAsset = "RazorAppWithPackageAndP2PReference";
            ProjectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var scopedCssFile = Path.Combine(ProjectDirectory.Path, "AppWithPackageAndP2PReference", "Index.razor.css");
            File.WriteAllText(scopedCssFile, "/* Empty css */");
            File.WriteAllText(Path.Combine(ProjectDirectory.Path, "AppWithPackageAndP2PReference", "Index.razor"), "This is a test razor component.");

            EnsureLocalPackagesExists();

            var restore = CreateRestoreCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            ExecuteCommand(restore).Should().Pass();

            var build = CreateBuildCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            ExecuteCommand(build).Should().Pass();

            var intermediateOutputPath = build.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var bundlePath = Path.Combine(intermediateOutputPath, "scopedcss", "bundle", "AppWithPackageAndP2PReference.styles.css");

            new FileInfo(bundlePath).Should().Exist();

            // Make an edit to a scoped css file
            File.WriteAllLines(scopedCssFile, File.ReadAllLines(scopedCssFile).Concat(new[] { "body { background-color: orangered; }" }));

            build = CreateBuildCommand(ProjectDirectory, "AppWithPackageAndP2PReference");
            ExecuteCommand(build, "/t:UpdateStaticWebAssetsDesignTime").Should().Pass();

            var fileInfo = new FileInfo(bundlePath);
            fileInfo.Should().Exist();
            // Verify the generated file contains newly added css
            var text = fileInfo.ReadAllText();
            text.Should().Contain("background-color: orangered");
            text.Should().MatchRegex(""".*@import '_content/ClassLibrary/ClassLibrary\.[a-zA-Z0-9]+\.bundle\.scp\.css.*""");
        }
    }
}
