// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class StaticWebAssetsPackIntegrationTest(ITestOutputHelper log)
        : IsolatedNuGetPackageFolderAspNetSdkBaselineTest(log, nameof(StaticWebAssetsPackIntegrationTest))
    {
        [Fact]
        public void Pack_FailsWhenStaticWebAssetsHaveConflictingPaths()
        {
            var testAsset = "PackageLibraryDirectDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages")
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var itemGroup = new XElement(ns + "ItemGroup");
                    var element = new XElement("StaticWebAsset", new XAttribute("Include", @"bundle\js\pkg-direct-dep.js"));
                    element.Add(new XElement("SourceType"));
                    element.Add(new XElement("SourceId", "PackageLibraryDirectDependency"));
                    element.Add(new XElement("ContentRoot", "$([MSBuild]::NormalizeDirectory('$(MSBuildProjectDirectory)\\bundle\\'))"));
                    element.Add(new XElement("BasePath", "_content/PackageLibraryDirectDependency"));
                    element.Add(new XElement("RelativePath", "js/pkg-direct-dep.js"));
                    itemGroup.Add(element);
                    project.Root.Add(itemGroup);
                });

            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "bundle", "js"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "bundle", "js", "pkg-direct-dep.js"), "console.log('bundle');");

            var pack = CreatePackCommand(projectDirectory, "PackageLibraryDirectDependency");
            ExecuteCommand(pack).Should().Fail();
        }

        // If you modify this test, make sure you also modify the test below this one to assert that things are not included as content.
        [Fact]
        public void Pack_IncludesStaticWebAssets()
        {
            var testAsset = "PackageLibraryDirectDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            var pack = CreatePackCommand(projectDirectory, "PackageLibraryDirectDependency");
            var result = ExecuteCommand(pack);

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryDirectDependency.dll")).Should().Exist();

            result.Should().NuPkgContainsPatterns(
                Path.Combine(pack.GetPackageDirectory().FullName, "PackageLibraryDirectDependency.1.0.0.nupkg"),
                filePatterns: new[]
                {
                    Path.Combine("staticwebassets", "js", "pkg-direct-dep.js"),
                    Path.Combine("staticwebassets", "css", "site.css"),
                    Path.Combine("staticwebassets", "PackageLibraryDirectDependency.*.bundle.scp.css"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryDirectDependency.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryDirectDependency.props"),
                    Path.Combine("buildTransitive", "PackageLibraryDirectDependency.props")
                });
        }

        [Fact]
        public void Pack_NoAssets_DoesNothing()
        {
            var testAsset = "PackageLibraryNoStaticAssets";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            var pack = CreatePackCommand(projectDirectory);
            var result = ExecuteCommand(pack);

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryNoStaticAssets.dll")).Should().Exist();

            result.Should().NuPkgDoesNotContain(
                Path.Combine(pack.GetPackageDirectory().FullName, "PackageLibraryNoStaticAssets.1.0.0.nupkg"),
                filePaths: new[]
                {
                    Path.Combine("staticwebassets"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryNoStaticAssets.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryNoStaticAssets.props"),
                    Path.Combine("buildTransitive", "PackageLibraryNoStaticAssets.props")
                });
        }

        [Fact]
        public void Pack_NoAssets_Multitargeting_DoesNothing()
        {
            var testAsset = "PackageLibraryNoStaticAssets";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges(project =>
            {
                var tfm = project.Root.Descendants("TargetFramework").Single();
                tfm.Name = "TargetFrameworks";
                tfm.Value = "net6.0;" + DefaultTfm;
            });

            var pack = CreatePackCommand(projectDirectory);
            var result = ExecuteCommand(pack);

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryNoStaticAssets.dll")).Should().Exist();

            result.Should().NuPkgDoesNotContain(
                Path.Combine(projectDirectory.Path, "bin", "Debug", "PackageLibraryNoStaticAssets.1.0.0.nupkg"),
                filePaths: new[]
                {
                    Path.Combine("staticwebassets"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryNoStaticAssets.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryNoStaticAssets.props"),
                    Path.Combine("buildTransitive", "PackageLibraryNoStaticAssets.props")
                });
        }

        [Fact]
        public void Pack_Incremental_IncludesStaticWebAssets()
        {
            var testAsset = "PackageLibraryDirectDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            var pack = CreatePackCommand(projectDirectory, "PackageLibraryDirectDependency");
            var result = ExecuteCommand(pack);

            result.Should().Pass();

            var pack2 = CreatePackCommand(projectDirectory, "PackageLibraryDirectDependency");
            var result2 = ExecuteCommand(pack2);

            result2.Should().Pass();

            var outputPath = pack2.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryDirectDependency.dll")).Should().Exist();

            result2.Should().NuPkgContainsPatterns(
                Path.Combine(pack2.GetPackageDirectory().FullName, "PackageLibraryDirectDependency.1.0.0.nupkg"),
                filePatterns: new[]
                {
                    Path.Combine("staticwebassets", "js", "pkg-direct-dep.js"),
                    Path.Combine("staticwebassets", "css", "site.css"),
                    Path.Combine("staticwebassets", "PackageLibraryDirectDependency.*.bundle.scp.css"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryDirectDependency.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryDirectDependency.props"),
                    Path.Combine("buildTransitive", "PackageLibraryDirectDependency.props")
                });
        }

        [Fact]
        public void Pack_StaticWebAssets_WithoutFileExtension_AreCorrectlyPacked()
        {
            var testAsset = "PackageLibraryDirectDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            File.WriteAllText(Path.Combine(projectDirectory.Path, "PackageLibraryDirectDependency", "wwwroot", "LICENSE"), "license file contents");

            var pack = CreatePackCommand(projectDirectory, "PackageLibraryDirectDependency");
            var result = ExecuteCommand(pack);

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryDirectDependency.dll")).Should().Exist();

            result.Should().NuPkgContainsPatterns(
                Path.Combine(pack.GetPackageDirectory().FullName, "PackageLibraryDirectDependency.1.0.0.nupkg"),
                filePatterns: new[]
                {
                    Path.Combine("staticwebassets", "js", "pkg-direct-dep.js"),
                    Path.Combine("staticwebassets", "css", "site.css"),
                    Path.Combine("staticwebassets", "LICENSE"),
                    Path.Combine("staticwebassets", "PackageLibraryDirectDependency.*.bundle.scp.css"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryDirectDependency.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryDirectDependency.props"),
                    Path.Combine("buildTransitive", "PackageLibraryDirectDependency.props")
                });
        }

        [Fact]
        public void Pack_MultipleTargetFrameworks_Works()
        {
            var testAsset = "PackageLibraryDirectDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges((project, document) =>
            {
                var tfm = document.Descendants("TargetFramework").Single();
                tfm.Name = "TargetFrameworks";
                tfm.FirstNode.ReplaceWith(tfm.FirstNode.ToString() + ";netstandard2.1");

                document.Descendants("AddRazorSupportForMvc").SingleOrDefault()?.Remove();
                document.Descendants("FrameworkReference").SingleOrDefault()?.Remove();
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "PackageLibraryDirectDependency", "Components"), recursive: true);

            var pack = CreatePackCommand(projectDirectory, "PackageLibraryDirectDependency");
            var result = ExecuteCommand(pack);

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryDirectDependency.dll")).Should().Exist();

            result.Should().NuPkgContain(
                Path.Combine(projectDirectory.Path, "PackageLibraryDirectDependency", "bin", "Debug", "PackageLibraryDirectDependency.1.0.0.nupkg"),
                filePaths: new[]
                {
                    Path.Combine("staticwebassets", "js", "pkg-direct-dep.js"),
                    Path.Combine("staticwebassets", "css", "site.css"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryDirectDependency.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryDirectDependency.props"),
                    Path.Combine("buildTransitive", "PackageLibraryDirectDependency.props")
                });
        }

        [Fact]
        public void Pack_MultipleTargetFrameworks_NoBuild_IncludesStaticWebAssets()
        {
            var testAsset = "PackageLibraryDirectDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges((project, document) =>
            {
                var tfm = document.Descendants("TargetFramework").Single();
                tfm.Name = "TargetFrameworks";
                tfm.FirstNode.ReplaceWith(tfm.FirstNode.ToString() + ";netstandard2.1");

                document.Descendants("AddRazorSupportForMvc").SingleOrDefault()?.Remove();
                document.Descendants("FrameworkReference").SingleOrDefault()?.Remove();
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "PackageLibraryDirectDependency", "Components"), recursive: true);

            var build = CreateBuildCommand(projectDirectory, "PackageLibraryDirectDependency");
            var buildResult = build.Execute();

            var pack = CreatePackCommand(projectDirectory, "PackageLibraryDirectDependency");
            var result = pack.Execute("/p:NoBuild=true");

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryDirectDependency.dll")).Should().Exist();

            result.Should().NuPkgContain(
                Path.Combine(projectDirectory.Path, "PackageLibraryDirectDependency", "bin", "Debug", "PackageLibraryDirectDependency.1.0.0.nupkg"),
                filePaths: new[]
                {
                    Path.Combine("staticwebassets", "js", "pkg-direct-dep.js"),
                    Path.Combine("staticwebassets", "css", "site.css"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryDirectDependency.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryDirectDependency.props"),
                    Path.Combine("buildTransitive", "PackageLibraryDirectDependency.props")
                });
        }

        [Fact]
        public void Pack_MultipleTargetFrameworks_NoBuild_DoesNotIncludeAssetsAsContent()
        {
            var testAsset = "PackageLibraryDirectDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges((project, document) =>
            {
                var tfm = document.Descendants("TargetFramework").Single();
                tfm.Name = "TargetFrameworks";
                tfm.FirstNode.ReplaceWith(tfm.FirstNode.ToString() + ";netstandard2.1");

                document.Descendants("AddRazorSupportForMvc").SingleOrDefault()?.Remove();
                document.Descendants("FrameworkReference").SingleOrDefault()?.Remove();
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "PackageLibraryDirectDependency", "Components"), recursive: true);

            var build = CreateBuildCommand(projectDirectory, "PackageLibraryDirectDependency");
            var buildResult = build.Execute();

            var pack = CreatePackCommand(projectDirectory, "PackageLibraryDirectDependency");
            var result = pack.Execute("/p:NoBuild=true");

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryDirectDependency.dll")).Should().Exist();

            result.Should().NuPkgDoesNotContain(
                Path.Combine(projectDirectory.Path, "PackageLibraryDirectDependency", "bin", "Debug", "PackageLibraryDirectDependency.1.0.0.nupkg"),
                filePaths: new[]
                {
                    Path.Combine("content", "wwwroot", "js", "pkg-direct-dep.js"),
                    Path.Combine("content", "wwwroot", "css", "site.css"),
                    Path.Combine("contentFiles", "wwwroot", "js", "pkg-direct-dep.js"),
                    Path.Combine("contentFiles", "wwwroot", "css", "site.css"),
                });
        }

        [Fact]
        public void Pack_MultipleTargetFrameworks_GeneratePackageOnBuild_IncludesStaticWebAssets()
        {
            var testAsset = "PackageLibraryDirectDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges((project, document) =>
            {
                var tfm = document.Descendants("TargetFramework").Single();
                tfm.Name = "TargetFrameworks";
                tfm.FirstNode.ReplaceWith(tfm.FirstNode.ToString() + ";netstandard2.1");

                document.Descendants("AddRazorSupportForMvc").SingleOrDefault()?.Remove();
                document.Descendants("FrameworkReference").SingleOrDefault()?.Remove();
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "PackageLibraryDirectDependency", "Components"), recursive: true);

            var build = CreateBuildCommand(projectDirectory, "PackageLibraryDirectDependency");
            var result = build.Execute("/p:GeneratePackageOnBuild=true");

            result.Should().Pass();

            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryDirectDependency.dll")).Should().Exist();

            result.Should().NuPkgContain(
                Path.Combine(projectDirectory.Path, "PackageLibraryDirectDependency", "bin", "Debug", "PackageLibraryDirectDependency.1.0.0.nupkg"),
                filePaths: new[]
                {
                    Path.Combine("staticwebassets", "js", "pkg-direct-dep.js"),
                    Path.Combine("staticwebassets", "css", "site.css"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryDirectDependency.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryDirectDependency.props"),
                    Path.Combine("buildTransitive", "PackageLibraryDirectDependency.props")
                });
        }

        [Fact]
        public void Pack_MultipleTargetFrameworks_GeneratePackageOnBuild_DoesNotIncludeAssetsAsContent()
        {
            var testAsset = "PackageLibraryDirectDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges((project, document) =>
            {
                var tfm = document.Descendants("TargetFramework").Single();
                tfm.Name = "TargetFrameworks";
                tfm.FirstNode.ReplaceWith(tfm.FirstNode.ToString() + ";netstandard2.1");

                document.Descendants("AddRazorSupportForMvc").SingleOrDefault()?.Remove();
                document.Descendants("FrameworkReference").SingleOrDefault()?.Remove();
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "PackageLibraryDirectDependency", "Components"), recursive: true);

            var build = CreateBuildCommand(projectDirectory, "PackageLibraryDirectDependency");
            var result = build.Execute("/p:GeneratePackageOnBuild=true");

            result.Should().Pass();

            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryDirectDependency.dll")).Should().Exist();

            result.Should().NuPkgDoesNotContain(
                Path.Combine(projectDirectory.Path, "PackageLibraryDirectDependency", "bin", "Debug", "PackageLibraryDirectDependency.1.0.0.nupkg"),
                filePaths: new[]
                {
                    Path.Combine("content", "wwwroot", "js", "pkg-direct-dep.js"),
                    Path.Combine("content", "wwwroot", "css", "site.css"),
                    Path.Combine("contentFiles", "wwwroot", "js", "pkg-direct-dep.js"),
                    Path.Combine("contentFiles", "wwwroot", "css", "site.css"),
                });
        }

        [Fact]
        public void Pack_BeforeNet60_MultipleTargetFrameworks_WithScopedCss_IncludesAssetsAndProjectBundle()
        {
            var testAsset = "PackageLibraryTransitiveDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges(document =>
            {
                var parse = XDocument.Parse($@"<Project Sdk=""Microsoft.NET.Sdk.Razor"">

  <PropertyGroup>
    <TargetFrameworks>netstandard2.0;net5.0</TargetFrameworks>
    <RazorLangVersion>3.0</RazorLangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Condition=""'$(TargetFramework)' == 'net5.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""{DefaultPackageVersion}"" />
    <PackageReference Condition=""'$(TargetFramework)' == 'netstandard2.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""3.1.0"" />
  </ItemGroup>

</Project>
");
                document.Root.ReplaceWith(parse.Root);
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "wwwroot"), recursive: true);

            var componentText = @"<div class=""my-component"">
    This component is defined in the <strong>razorclasslibrarypack</strong> library.
</div>";

            // This mimics the structure of our default template project
            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "_Imports.razor"), "@using Microsoft.AspNetCore.Components.Web" + Environment.NewLine);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor"), componentText);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.css"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "ExampleJsInterop.cs"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "background.png"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "exampleJsInterop.js"), "");

            var pack = CreatePackCommand(projectDirectory);
            var result = ExecuteCommand(pack);

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryTransitiveDependency.dll")).Should().Exist();

            var packagePath = Path.Combine(
                projectDirectory.Path,
                "bin",
                "Debug",
                "PackageLibraryTransitiveDependency.1.0.0.nupkg");

            result.Should().NuPkgContainsPatterns(
                packagePath,
                filePatterns: new[]
                {
                    Path.Combine("staticwebassets", "exampleJsInterop.js"),
                    Path.Combine("staticwebassets", "background.png"),
                    Path.Combine("staticwebassets", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryTransitiveDependency.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryTransitiveDependency.props"),
                    Path.Combine("buildTransitive", "PackageLibraryTransitiveDependency.props")
                });
        }

        [Fact]
        public void Pack_BeforeNet60_MultipleTargetFrameworks_WithScopedCss_DoesNotIncludeAssetsAsContent()
        {
            var testAsset = "PackageLibraryTransitiveDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges(document =>
            {
                var parse = XDocument.Parse($@"<Project Sdk=""Microsoft.NET.Sdk.Razor"">

  <PropertyGroup>
    <TargetFrameworks>netstandard2.0;net5.0</TargetFrameworks>
    <RazorLangVersion>3.0</RazorLangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Condition=""'$(TargetFramework)' == 'net5.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""{DefaultPackageVersion}"" />
    <PackageReference Condition=""'$(TargetFramework)' == 'netstandard2.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""3.1.0"" />
  </ItemGroup>

</Project>
");
                document.Root.ReplaceWith(parse.Root);
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "wwwroot"), recursive: true);

            var componentText = @"<div class=""my-component"">
    This component is defined in the <strong>razorclasslibrarypack</strong> library.
</div>";

            // This mimics the structure of our default template project
            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "_Imports.razor"), "@using Microsoft.AspNetCore.Components.Web" + Environment.NewLine);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor"), componentText);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.css"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "ExampleJsInterop.cs"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "background.png"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "exampleJsInterop.js"), "");

            var pack = CreatePackCommand(projectDirectory);
            var result = ExecuteCommand(pack);

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryTransitiveDependency.dll")).Should().Exist();

            var packagePath = Path.Combine(
                projectDirectory.Path,
                "bin",
                "Debug",
                "PackageLibraryTransitiveDependency.1.0.0.nupkg");

            result.Should().NuPkgDoesNotContain(
                packagePath,
                filePaths: new[]
                {
                    Path.Combine("content", "exampleJsInterop.js"),
                    Path.Combine("content", "background.png"),
                    Path.Combine("content", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                    Path.Combine("contentFiles", "exampleJsInterop.js"),
                    Path.Combine("contentFiles", "background.png"),
                    Path.Combine("contentFiles", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                });
        }

        [Fact]
        public void Pack_BeforeNet60_MultipleTargetFrameworks_NoBuild_WithScopedCss_IncludesAssetsAndProjectBundle()
        {
            var testAsset = "PackageLibraryTransitiveDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges(document =>
            {
                var parse = XDocument.Parse($@"<Project Sdk=""Microsoft.NET.Sdk.Razor"">

  <PropertyGroup>
    <TargetFrameworks>netstandard2.0;net5.0</TargetFrameworks>
    <RazorLangVersion>3.0</RazorLangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Condition=""'$(TargetFramework)' == 'net5.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""{DefaultPackageVersion}"" />
    <PackageReference Condition=""'$(TargetFramework)' == 'netstandard2.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""3.1.0"" />
  </ItemGroup>

</Project>
");
                document.Root.ReplaceWith(parse.Root);
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "wwwroot"), recursive: true);

            var componentText = @"<div class=""my-component"">
    This component is defined in the <strong>razorclasslibrarypack</strong> library.
</div>";

            // This mimics the structure of our default template project
            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "_Imports.razor"), "@using Microsoft.AspNetCore.Components.Web" + Environment.NewLine);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor"), componentText);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.css"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "ExampleJsInterop.cs"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "background.png"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "exampleJsInterop.js"), "");

            var build = CreateBuildCommand(projectDirectory);
            var buildResult = build.Execute();

            buildResult.Should().Pass();

            var pack = CreatePackCommand(projectDirectory);
            var result = pack.Execute("/p:NoBuild=true");

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryTransitiveDependency.dll")).Should().Exist();

            var packagePath = Path.Combine(
                projectDirectory.Path,
                "bin",
                "Debug",
                "PackageLibraryTransitiveDependency.1.0.0.nupkg");

            result.Should().NuPkgContain(
                packagePath,
                filePaths: new[]
                {
                    Path.Combine("staticwebassets", "exampleJsInterop.js"),
                    Path.Combine("staticwebassets", "background.png"),
                    Path.Combine("staticwebassets", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryTransitiveDependency.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryTransitiveDependency.props"),
                    Path.Combine("buildTransitive", "PackageLibraryTransitiveDependency.props")
                });
        }

        [Fact]
        public void Pack_BeforeNet60_MultipleTargetFrameworks_NoBuild_WithScopedCss_DoesNotIncludeAssetsAsContent()
        {
            var testAsset = "PackageLibraryTransitiveDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges(document =>
            {
                var parse = XDocument.Parse($@"<Project Sdk=""Microsoft.NET.Sdk.Razor"">

  <PropertyGroup>
    <TargetFrameworks>netstandard2.0;net5.0</TargetFrameworks>
    <RazorLangVersion>3.0</RazorLangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Condition=""'$(TargetFramework)' == 'net5.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""{DefaultPackageVersion}"" />
    <PackageReference Condition=""'$(TargetFramework)' == 'netstandard2.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""3.1.0"" />
  </ItemGroup>

</Project>
");
                document.Root.ReplaceWith(parse.Root);
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "wwwroot"), recursive: true);

            var componentText = @"<div class=""my-component"">
    This component is defined in the <strong>razorclasslibrarypack</strong> library.
</div>";

            // This mimics the structure of our default template project
            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "_Imports.razor"), "@using Microsoft.AspNetCore.Components.Web" + Environment.NewLine);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor"), componentText);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.css"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "ExampleJsInterop.cs"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "background.png"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "exampleJsInterop.js"), "");

            var build = CreateBuildCommand(projectDirectory);
            var buildResult = build.Execute();

            buildResult.Should().Pass();

            var pack = CreatePackCommand(projectDirectory);
            var result = pack.Execute("/p:NoBuild=true");

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryTransitiveDependency.dll")).Should().Exist();

            var packagePath = Path.Combine(
                projectDirectory.Path,
                "bin",
                "Debug",
                "PackageLibraryTransitiveDependency.1.0.0.nupkg");

            result.Should().NuPkgDoesNotContain(
                packagePath,
                filePaths: new[]
                {
                    Path.Combine("content", "exampleJsInterop.js"),
                    Path.Combine("content", "background.png"),
                    Path.Combine("content", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                    Path.Combine("contentFiles", "exampleJsInterop.js"),
                    Path.Combine("contentFiles", "background.png"),
                    Path.Combine("contentFiles", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                });
        }

        [Fact]
        public void Pack_BeforeNet60_MultipleTargetFrameworks_GeneratePackageOnBuild_WithScopedCss_IncludesAssetsAndProjectBundle()
        {
            var testAsset = "PackageLibraryTransitiveDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges(document =>
            {
                var parse = XDocument.Parse($@"<Project Sdk=""Microsoft.NET.Sdk.Razor"">

  <PropertyGroup>
    <TargetFrameworks>netstandard2.0;net5.0</TargetFrameworks>
    <RazorLangVersion>3.0</RazorLangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Condition=""'$(TargetFramework)' == 'net5.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""{DefaultPackageVersion}"" />
    <PackageReference Condition=""'$(TargetFramework)' == 'netstandard2.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""3.1.0"" />
  </ItemGroup>

</Project>
");
                document.Root.ReplaceWith(parse.Root);
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "wwwroot"), recursive: true);

            var componentText = @"<div class=""my-component"">
    This component is defined in the <strong>razorclasslibrarypack</strong> library.
</div>";

            // This mimics the structure of our default template project
            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "_Imports.razor"), "@using Microsoft.AspNetCore.Components.Web" + Environment.NewLine);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor"), componentText);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.css"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "ExampleJsInterop.cs"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "background.png"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "exampleJsInterop.js"), "");

            var build = CreateBuildCommand(projectDirectory);
            var result = build.Execute("/p:GeneratePackageOnBuild=true");

            result.Should().Pass();

            var outputPath = build.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryTransitiveDependency.dll")).Should().Exist();

            var packagePath = Path.Combine(
                projectDirectory.Path,
                "bin",
                "Debug",
                "PackageLibraryTransitiveDependency.1.0.0.nupkg");

            result.Should().NuPkgContain(
                packagePath,
                filePaths: new[]
                {
                    Path.Combine("staticwebassets", "exampleJsInterop.js"),
                    Path.Combine("staticwebassets", "background.png"),
                    Path.Combine("staticwebassets", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryTransitiveDependency.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryTransitiveDependency.props"),
                    Path.Combine("buildTransitive", "PackageLibraryTransitiveDependency.props")
                });
        }

        [Fact]
        public void Pack_BeforeNet60_MultipleTargetFrameworks_GeneratePackageOnBuild_WithScopedCss_DoesNotIncludeAssetsAsContent()
        {
            var testAsset = "PackageLibraryTransitiveDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges(document =>
            {
                var parse = XDocument.Parse($@"<Project Sdk=""Microsoft.NET.Sdk.Razor"">

  <PropertyGroup>
    <TargetFrameworks>netstandard2.0;net5.0</TargetFrameworks>
    <RazorLangVersion>3.0</RazorLangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Condition=""'$(TargetFramework)' == 'net5.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""{DefaultPackageVersion}"" />
    <PackageReference Condition=""'$(TargetFramework)' == 'netstandard2.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""3.1.0"" />
  </ItemGroup>

</Project>
");
                document.Root.ReplaceWith(parse.Root);
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "wwwroot"), recursive: true);

            var componentText = @"<div class=""my-component"">
    This component is defined in the <strong>razorclasslibrarypack</strong> library.
</div>";

            // This mimics the structure of our default template project
            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "_Imports.razor"), "@using Microsoft.AspNetCore.Components.Web" + Environment.NewLine);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor"), componentText);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.css"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "ExampleJsInterop.cs"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "background.png"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "exampleJsInterop.js"), "");

            var build = CreateBuildCommand(projectDirectory);
            var result = build.Execute("/p:GeneratePackageOnBuild=true");

            result.Should().Pass();

            var outputPath = build.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryTransitiveDependency.dll")).Should().Exist();

            var packagePath = Path.Combine(
                projectDirectory.Path,
                "bin",
                "Debug",
                "PackageLibraryTransitiveDependency.1.0.0.nupkg");

            result.Should().NuPkgDoesNotContain(
                packagePath,
                filePaths: new[]
                {
                    Path.Combine("content", "exampleJsInterop.js"),
                    Path.Combine("content", "background.png"),
                    Path.Combine("content", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                    Path.Combine("contentFiles", "exampleJsInterop.js"),
                    Path.Combine("contentFiles", "background.png"),
                    Path.Combine("contentFiles", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                });
        }

        [Fact]
        public void Pack_Net50_WithScopedCss_IncludesAssetsAndProjectBundle()
        {
            var testAsset = "PackageLibraryTransitiveDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges(document =>
            {
                var parse = XDocument.Parse($@"<Project Sdk=""Microsoft.NET.Sdk.Razor"">

  <PropertyGroup>
    <TargetFramework>net5.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Condition=""'$(TargetFramework)' == 'net5.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""{DefaultPackageVersion}"" />
  </ItemGroup>

</Project>
");
                document.Root.ReplaceWith(parse.Root);
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "wwwroot"), recursive: true);

            var componentText = @"<div class=""my-component"">
    This component is defined in the <strong>razorclasslibrarypack</strong> library.
</div>";

            // This mimics the structure of our default template project
            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "_Imports.razor"), "@using Microsoft.AspNetCore.Components.Web" + Environment.NewLine);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor"), componentText);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.css"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "ExampleJsInterop.cs"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "background.png"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "exampleJsInterop.js"), "");

            var pack = CreatePackCommand(projectDirectory);
            var result = ExecuteCommand(pack);

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryTransitiveDependency.dll")).Should().Exist();

            var packagePath = Path.Combine(
                projectDirectory.Path,
                "bin",
                "Debug",
                "PackageLibraryTransitiveDependency.1.0.0.nupkg");

            result.Should().NuPkgContain(
                packagePath,
                filePaths: new[]
                {
                    Path.Combine("staticwebassets", "exampleJsInterop.js"),
                    Path.Combine("staticwebassets", "background.png"),
                    Path.Combine("staticwebassets", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryTransitiveDependency.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryTransitiveDependency.props"),
                    Path.Combine("buildTransitive", "PackageLibraryTransitiveDependency.props")
                });
        }

        [Fact]
        public void Pack_Net50_WithScopedCss_DoesNotIncludeAssetsAsContent()
        {
            var testAsset = "PackageLibraryTransitiveDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges(document =>
            {
                var parse = XDocument.Parse($@"<Project Sdk=""Microsoft.NET.Sdk.Razor"">

  <PropertyGroup>
    <TargetFramework>net5.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Condition=""'$(TargetFramework)' == 'net5.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""{DefaultPackageVersion}"" />
  </ItemGroup>

</Project>
");
                document.Root.ReplaceWith(parse.Root);
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "wwwroot"), recursive: true);

            var componentText = @"<div class=""my-component"">
    This component is defined in the <strong>razorclasslibrarypack</strong> library.
</div>";

            // This mimics the structure of our default template project
            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "_Imports.razor"), "@using Microsoft.AspNetCore.Components.Web" + Environment.NewLine);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor"), componentText);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.css"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "ExampleJsInterop.cs"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "background.png"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "exampleJsInterop.js"), "");

            var pack = CreatePackCommand(projectDirectory);
            var result = ExecuteCommand(pack);

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryTransitiveDependency.dll")).Should().Exist();

            var packagePath = Path.Combine(
                projectDirectory.Path,
                "bin",
                "Debug",
                "PackageLibraryTransitiveDependency.1.0.0.nupkg");

            result.Should().NuPkgDoesNotContain(
                packagePath,
                filePaths: new[]
                {
                    Path.Combine("content", "exampleJsInterop.js"),
                    Path.Combine("content", "background.png"),
                    Path.Combine("content", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                    Path.Combine("contentFiles", "exampleJsInterop.js"),
                    Path.Combine("contentFiles", "background.png"),
                    Path.Combine("contentFiles", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                });
        }

        [Fact]
        public void Pack_Net50_NoBuild_WithScopedCss_IncludesAssetsAndProjectBundle()
        {
            var testAsset = "PackageLibraryTransitiveDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges(document =>
            {
                var parse = XDocument.Parse($@"<Project Sdk=""Microsoft.NET.Sdk.Razor"">

  <PropertyGroup>
    <TargetFramework>net5.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Condition=""'$(TargetFramework)' == 'net5.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""{DefaultPackageVersion}"" />
  </ItemGroup>

</Project>
");
                document.Root.ReplaceWith(parse.Root);
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "wwwroot"), recursive: true);

            var componentText = @"<div class=""my-component"">
    This component is defined in the <strong>razorclasslibrarypack</strong> library.
</div>";

            // This mimics the structure of our default template project
            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "_Imports.razor"), "@using Microsoft.AspNetCore.Components.Web" + Environment.NewLine);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor"), componentText);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.css"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "ExampleJsInterop.cs"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "background.png"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "exampleJsInterop.js"), "");

            var build = CreateBuildCommand(projectDirectory);
            var buildResult = build.Execute();

            buildResult.Should().Pass();

            var pack = CreatePackCommand(projectDirectory);
            var result = pack.Execute("/p:NoBuild=true");

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryTransitiveDependency.dll")).Should().Exist();

            var packagePath = Path.Combine(
                projectDirectory.Path,
                "bin",
                "Debug",
                "PackageLibraryTransitiveDependency.1.0.0.nupkg");

            result.Should().NuPkgContain(
                packagePath,
                filePaths: new[]
                {
                    Path.Combine("staticwebassets", "exampleJsInterop.js"),
                    Path.Combine("staticwebassets", "background.png"),
                    Path.Combine("staticwebassets", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryTransitiveDependency.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryTransitiveDependency.props"),
                    Path.Combine("buildTransitive", "PackageLibraryTransitiveDependency.props")
                });
        }

        [Fact]
        public void Pack_Net50_NoBuild_WithScopedCss_DoesNotIncludeAssetsAsContent()
        {
            var testAsset = "PackageLibraryTransitiveDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges(document =>
            {
                var parse = XDocument.Parse($@"<Project Sdk=""Microsoft.NET.Sdk.Razor"">

  <PropertyGroup>
    <TargetFramework>net5.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Condition=""'$(TargetFramework)' == 'net5.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""{DefaultPackageVersion}"" />
  </ItemGroup>

</Project>
");
                document.Root.ReplaceWith(parse.Root);
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "wwwroot"), recursive: true);

            var componentText = @"<div class=""my-component"">
    This component is defined in the <strong>razorclasslibrarypack</strong> library.
</div>";

            // This mimics the structure of our default template project
            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "_Imports.razor"), "@using Microsoft.AspNetCore.Components.Web" + Environment.NewLine);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor"), componentText);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.css"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "ExampleJsInterop.cs"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "background.png"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "exampleJsInterop.js"), "");

            var build = CreateBuildCommand(projectDirectory);
            var buildResult = build.Execute();

            buildResult.Should().Pass();

            var pack = CreatePackCommand(projectDirectory);
            var result = pack.Execute("/p:NoBuild=true");

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryTransitiveDependency.dll")).Should().Exist();

            var packagePath = Path.Combine(
                projectDirectory.Path,
                "bin",
                "Debug",
                "PackageLibraryTransitiveDependency.1.0.0.nupkg");

            result.Should().NuPkgDoesNotContain(
                packagePath,
                filePaths: new[]
                {
                    Path.Combine("content", "exampleJsInterop.js"),
                    Path.Combine("content", "background.png"),
                    Path.Combine("content", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                    Path.Combine("contentFiles", "exampleJsInterop.js"),
                    Path.Combine("contentFiles", "background.png"),
                    Path.Combine("contentFiles", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                });
        }

        [Fact]
        public void Pack_Net50_GeneratePackageOnBuild_WithScopedCss_IncludesAssetsAndProjectBundle()
        {
            var testAsset = "PackageLibraryTransitiveDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges(document =>
            {
                var parse = XDocument.Parse($@"<Project Sdk=""Microsoft.NET.Sdk.Razor"">

  <PropertyGroup>
    <TargetFramework>net5.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Condition=""'$(TargetFramework)' == 'net5.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""{DefaultPackageVersion}"" />
  </ItemGroup>

</Project>
");
                document.Root.ReplaceWith(parse.Root);
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "wwwroot"), recursive: true);

            var componentText = @"<div class=""my-component"">
    This component is defined in the <strong>razorclasslibrarypack</strong> library.
</div>";

            // This mimics the structure of our default template project
            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "_Imports.razor"), "@using Microsoft.AspNetCore.Components.Web" + Environment.NewLine);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor"), componentText);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.css"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "ExampleJsInterop.cs"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "background.png"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "exampleJsInterop.js"), "");

            var build = CreateBuildCommand(projectDirectory);
            var result = build.Execute("/p:GeneratePackageOnBuild=true");

            result.Should().Pass();

            var outputPath = build.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryTransitiveDependency.dll")).Should().Exist();

            var packagePath = Path.Combine(
                projectDirectory.Path,
                "bin",
                "Debug",
                "PackageLibraryTransitiveDependency.1.0.0.nupkg");

            result.Should().NuPkgContain(
                packagePath,
                filePaths: new[]
                {
                    Path.Combine("staticwebassets", "exampleJsInterop.js"),
                    Path.Combine("staticwebassets", "background.png"),
                    Path.Combine("staticwebassets", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryTransitiveDependency.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryTransitiveDependency.props"),
                    Path.Combine("buildTransitive", "PackageLibraryTransitiveDependency.props")
                });
        }

        [Fact]
        public void Pack_Net50_GeneratePackageOnBuild_WithScopedCss_DoesNotIncludeAssetsAsContent()
        {
            var testAsset = "PackageLibraryTransitiveDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges(document =>
            {
                var parse = XDocument.Parse($@"<Project Sdk=""Microsoft.NET.Sdk.Razor"">

  <PropertyGroup>
    <TargetFramework>net5.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Condition=""'$(TargetFramework)' == 'net5.0'"" Include=""Microsoft.AspNetCore.Components.Web"" Version=""{DefaultPackageVersion}"" />
  </ItemGroup>

</Project>
");
                document.Root.ReplaceWith(parse.Root);
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "wwwroot"), recursive: true);

            var componentText = @"<div class=""my-component"">
    This component is defined in the <strong>razorclasslibrarypack</strong> library.
</div>";

            // This mimics the structure of our default template project
            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "_Imports.razor"), "@using Microsoft.AspNetCore.Components.Web" + Environment.NewLine);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor"), componentText);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.css"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "ExampleJsInterop.cs"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "background.png"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "exampleJsInterop.js"), "");

            var build = CreateBuildCommand(projectDirectory);
            var result = build.Execute("/p:GeneratePackageOnBuild=true");

            result.Should().Pass();

            var outputPath = build.GetOutputDirectory("net5.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryTransitiveDependency.dll")).Should().Exist();

            var packagePath = Path.Combine(
                projectDirectory.Path,
                "bin",
                "Debug",
                "PackageLibraryTransitiveDependency.1.0.0.nupkg");

            result.Should().NuPkgDoesNotContain(
                packagePath,
                filePaths: new[]
                {
                    Path.Combine("content", "exampleJsInterop.js"),
                    Path.Combine("content", "background.png"),
                    Path.Combine("content", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                    Path.Combine("contentFiles", "exampleJsInterop.js"),
                    Path.Combine("contentFiles", "background.png"),
                    Path.Combine("contentFiles", "PackageLibraryTransitiveDependency.bundle.scp.css"),
                });
        }

        [Fact]
        public void Pack_MultipleTargetFrameworks_WithScopedCssAndJsModules_IncludesAssetsAndProjectBundle()
        {
            var testAsset = "PackageLibraryTransitiveDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges(document =>
            {
                var parse = XDocument.Parse($@"<Project Sdk=""Microsoft.NET.Sdk.Razor"">

  <PropertyGroup>
    <TargetFrameworks>{ToolsetInfo.CurrentTargetFramework};net8.0;net7.0;net6.0;net5.0</TargetFrameworks>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <SupportedPlatform Condition=""'$(TargetFramework)' == 'net6.0' OR '$(TargetFramework)' == 'net7.0' OR '$(TargetFramework)' == 'net8.0' OR '$(TargetFramework)' == '{ToolsetInfo.CurrentTargetFramework}'"" Include=""browser"" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include=""Microsoft.AspNetCore.Components.Web"" Version=""{DefaultPackageVersion}"" />
  </ItemGroup>

</Project>
");
                document.Root.ReplaceWith(parse.Root);
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "wwwroot"), recursive: true);

            var componentText = @"<div class=""my-component"">
    This component is defined in the <strong>razorclasslibrarypack</strong> library.
</div>";

            // This mimics the structure of our default template project
            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "_Imports.razor"), "@using Microsoft.AspNetCore.Components.Web" + Environment.NewLine);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor"), componentText);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.css"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.js"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "ExampleJsInterop.cs"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "background.png"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "PackageLibraryTransitiveDependency.lib.module.js"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "exampleJsInterop.js"), "");

            var pack = CreatePackCommand(projectDirectory);
            var result = ExecuteCommand(pack);

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryTransitiveDependency.dll")).Should().Exist();

            var packagePath = Path.Combine(
                projectDirectory.Path,
                "bin",
                "Debug",
                "PackageLibraryTransitiveDependency.1.0.0.nupkg");

            result.Should().NuPkgContainsPatterns(
                packagePath,
                filePatterns: new[]
                {
                    Path.Combine("staticwebassets", "exampleJsInterop.js"),
                    Path.Combine("staticwebassets", "background.png"),
                    Path.Combine("staticwebassets", "Component1.razor.js"),
                    Path.Combine("staticwebassets", "PackageLibraryTransitiveDependency.*.bundle.scp.css"),
                    Path.Combine("staticwebassets", "PackageLibraryTransitiveDependency.*.lib.module.js"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryTransitiveDependency.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryTransitiveDependency.props"),
                    Path.Combine("buildTransitive", "PackageLibraryTransitiveDependency.props")
                });
        }

        [Fact]
        public void Pack_Incremental_MultipleTargetFrameworks_WithScopedCssAndJsModules_IncludesAssetsAndProjectBundle()
        {
            var testAsset = "PackageLibraryTransitiveDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges(document =>
            {
                var parse = XDocument.Parse($@"<Project Sdk=""Microsoft.NET.Sdk.Razor"">

  <PropertyGroup>
    <TargetFrameworks>{ToolsetInfo.CurrentTargetFramework};net8.0;net7.0;net6.0;net5.0</TargetFrameworks>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <SupportedPlatform Condition=""'$(TargetFramework)' == 'net6.0' OR '$(TargetFramework)' == 'net7.0' OR '$(TargetFramework)' == 'net8.0' OR '$(TargetFramework)' == '{ToolsetInfo.CurrentTargetFramework}'"" Include=""browser"" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include=""Microsoft.AspNetCore.Components.Web"" Version=""{DefaultPackageVersion}"" />
  </ItemGroup>

</Project>
");
                document.Root.ReplaceWith(parse.Root);
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "wwwroot"), recursive: true);

            var componentText = @"<div class=""my-component"">
    This component is defined in the <strong>razorclasslibrarypack</strong> library.
</div>";

            // This mimics the structure of our default template project
            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "_Imports.razor"), "@using Microsoft.AspNetCore.Components.Web" + Environment.NewLine);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor"), componentText);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.css"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.js"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "ExampleJsInterop.cs"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "background.png"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "PackageLibraryTransitiveDependency.lib.module.js"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "exampleJsInterop.js"), "");

            var pack = CreatePackCommand(projectDirectory);

            var pack2 = CreatePackCommand(projectDirectory);
            var result2 = pack2.Execute();

            result2.Should().Pass();

            var outputPath = pack2.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryTransitiveDependency.dll")).Should().Exist();

            var packagePath = Path.Combine(
                projectDirectory.Path,
                "bin",
                "Debug",
                "PackageLibraryTransitiveDependency.1.0.0.nupkg");

            result2.Should().NuPkgContainsPatterns(
                packagePath,
                filePatterns: new[]
                {
                    Path.Combine("staticwebassets", "exampleJsInterop.js"),
                    Path.Combine("staticwebassets", "background.png"),
                    Path.Combine("staticwebassets", "Component1.razor.js"),
                    Path.Combine("staticwebassets", "PackageLibraryTransitiveDependency.*.bundle.scp.css"),
                    Path.Combine("staticwebassets", "PackageLibraryTransitiveDependency.*.lib.module.js"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryTransitiveDependency.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryTransitiveDependency.props"),
                    Path.Combine("buildTransitive", "PackageLibraryTransitiveDependency.props")
                });
        }

        [Fact]
        public void Pack_MultipleTargetFrameworks_WithScopedCssAndJsModules_DoesNotIncludeApplicationBundleNorModulesManifest()
        {
            var testAsset = "PackageLibraryTransitiveDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges(document =>
            {
                var parse = XDocument.Parse($@"<Project Sdk=""Microsoft.NET.Sdk.Razor"">

  <PropertyGroup>
    <TargetFrameworks>{ToolsetInfo.CurrentTargetFramework};net8.0;net7.0;net6.0;net5.0</TargetFrameworks>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <SupportedPlatform Condition=""'$(TargetFramework)' == 'net6.0' OR '$(TargetFramework)' == 'net7.0' OR '$(TargetFramework)' == 'net8.0' OR '$(TargetFramework)' == '{ToolsetInfo.CurrentTargetFramework}'"" Include=""browser"" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include=""Microsoft.AspNetCore.Components.Web"" Version=""{DefaultPackageVersion}"" />
  </ItemGroup>

</Project>
");
                document.Root.ReplaceWith(parse.Root);
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "wwwroot"), recursive: true);

            var componentText = @"<div class=""my-component"">
    This component is defined in the <strong>razorclasslibrarypack</strong> library.
</div>";

            // This mimics the structure of our default template project
            Directory.CreateDirectory(Path.Combine(projectDirectory.Path, "wwwroot"));
            File.WriteAllText(Path.Combine(projectDirectory.Path, "_Imports.razor"), "@using Microsoft.AspNetCore.Components.Web" + Environment.NewLine);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor"), componentText);
            File.WriteAllText(Path.Combine(projectDirectory.Path, "Component1.razor.css"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "ExampleJsInterop.cs"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "background.png"), "");
            File.WriteAllText(Path.Combine(projectDirectory.Path, "wwwroot", "exampleJsInterop.js"), "");

            var pack = CreatePackCommand(projectDirectory);
            var result = ExecuteCommand(pack);

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryTransitiveDependency.dll")).Should().Exist();

            var packagePath = Path.Combine(
                projectDirectory.Path,
                "bin",
                "Debug",
                "PackageLibraryTransitiveDependency.1.0.0.nupkg");

            result.Should().NuPkgDoesNotContain(
                packagePath,
                filePaths: new[]
                {
                    Path.Combine("staticwebassets", "PackageLibraryTransitiveDependency.styles.css"),
                    Path.Combine("staticwebassets", "PackageLibraryTransitiveDependency.modules.json"),
                });
        }

        [Fact]
        public void Pack_MultipleTargetFrameworks_DoesNotIncludeAssetsAsContent()
        {
            var testAsset = "PackageLibraryDirectDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            projectDirectory.WithProjectChanges((project, document) =>
            {
                var tfm = document.Descendants("TargetFramework").Single();
                tfm.Name = "TargetFrameworks";
                tfm.FirstNode.ReplaceWith(tfm.FirstNode.ToString() + ";netstandard2.1");

                document.Descendants("AddRazorSupportForMvc").SingleOrDefault()?.Remove();
                document.Descendants("FrameworkReference").SingleOrDefault()?.Remove();
            });

            Directory.Delete(Path.Combine(projectDirectory.Path, "PackageLibraryDirectDependency", "Components"), recursive: true);

            var pack = CreatePackCommand(projectDirectory, "PackageLibraryDirectDependency");
            var result = ExecuteCommand(pack);

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryDirectDependency.dll")).Should().Exist();

            result.Should().NuPkgDoesNotContain(
                Path.Combine(projectDirectory.Path, "PackageLibraryDirectDependency", "bin", "Debug", "PackageLibraryDirectDependency.1.0.0.nupkg"),
                filePaths: new[]
                {
                    Path.Combine("content", "wwwroot", "js", "pkg-direct-dep.js"),
                    Path.Combine("content", "wwwroot", "css", "site.css"),
                    Path.Combine("contentFiles", "wwwroot", "js", "pkg-direct-dep.js"),
                    Path.Combine("contentFiles", "wwwroot", "css", "site.css"),
                });
        }

        [Fact]
        public void Pack_DoesNotInclude_TransitiveBundleOrScopedCssAsStaticWebAsset()
        {
            var testAsset = "PackageLibraryDirectDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            var pack = CreatePackCommand(projectDirectory, "PackageLibraryDirectDependency");
            var result = ExecuteCommand(pack);

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryDirectDependency.dll")).Should().Exist();

            result.Should().NuPkgDoesNotContain(
                Path.Combine(pack.GetPackageDirectory().FullName, "PackageLibraryDirectDependency.1.0.0.nupkg"),
                filePaths: new[]
                {
                    // This is to make sure we don't include the scoped css files on the package when bundling is enabled.
                    Path.Combine("staticwebassets", "Components", "App.razor.rz.scp.css"),
                    Path.Combine("staticwebassets", "PackageLibraryDirectDependency.styles.css"),
                });
        }

        [Fact]
        public void Pack_DoesNotIncludeStaticWebAssetsAsContent()
        {
            var testAsset = "PackageLibraryDirectDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            var pack = CreatePackCommand(projectDirectory, "PackageLibraryDirectDependency");
            var result = ExecuteCommand(pack);

            result.Should().Pass();

            var outputPath = pack.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryDirectDependency.dll")).Should().Exist();

            result.Should().NuPkgDoesNotContain(
                Path.Combine(pack.GetPackageDirectory().FullName, "PackageLibraryDirectDependency.1.0.0.nupkg"),
                filePaths: new[]
                {
                    Path.Combine("content", "wwwroot", "js", "pkg-direct-dep.js"),
                    Path.Combine("content", "wwwroot", "css", "site.css"),
                    Path.Combine("content", "Components", "App.razor.css"),
                    // This is to make sure we don't include the unscoped css file on the package.
                    Path.Combine("content", "Components", "App.razor.css"),
                    Path.Combine("content", "Components", "App.razor.rz.scp.css"),
                    Path.Combine("contentFiles", "wwwroot", "js", "pkg-direct-dep.js"),
                    Path.Combine("contentFiles", "wwwroot", "css", "site.css"),
                    Path.Combine("contentFiles", "Components", "App.razor.css"),
                    Path.Combine("contentFiles", "Components", "App.razor.rz.scp.css"),
                });
        }

        [Fact]
        public void Pack_NoBuild_IncludesStaticWebAssets()
        {
            var testAsset = "PackageLibraryDirectDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            var build = CreateBuildCommand(projectDirectory, "PackageLibraryDirectDependency");
            build.Execute().Should().Pass();

            var pack = CreatePackCommand(projectDirectory, "PackageLibraryDirectDependency");
            var result = pack.Execute("/p:NoBuild=true");

            var outputPath = pack.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryDirectDependency.dll")).Should().Exist();

            result.Should().NuPkgContainsPatterns(
                Path.Combine(build.GetPackageDirectory().FullName, "PackageLibraryDirectDependency.1.0.0.nupkg"),
                filePatterns: new[]
                {
                    Path.Combine("staticwebassets", "js", "pkg-direct-dep.js"),
                    Path.Combine("staticwebassets", "PackageLibraryDirectDependency.*.bundle.scp.css"),
                    Path.Combine("staticwebassets", "css", "site.css"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryDirectDependency.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryDirectDependency.props"),
                    Path.Combine("buildTransitive", "PackageLibraryDirectDependency.props")
                });
        }

        [Fact]
        public void Pack_NoBuild_DoesNotIncludeFilesAsContent()
        {
            var testAsset = "PackageLibraryDirectDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            var build = CreateBuildCommand(projectDirectory, "PackageLibraryDirectDependency");
            build.Execute().Should().Pass();

            var pack = CreatePackCommand(projectDirectory, "PackageLibraryDirectDependency");
            var result = pack.Execute("/p:NoBuild=true");

            var outputPath = pack.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryDirectDependency.dll")).Should().Exist();

            result.Should().NuPkgDoesNotContain(
                Path.Combine(pack.GetPackageDirectory().FullName, "PackageLibraryDirectDependency.1.0.0.nupkg"),
                filePaths: new[]
                {
                    Path.Combine("content", "wwwroot", "js", "pkg-direct-dep.js"),
                    Path.Combine("content", "PackageLibraryDirectDependency.bundle.scp.css"),
                    Path.Combine("content", "wwwroot", "css", "site.css"),
                    Path.Combine("contentFiles", "wwwroot", "js", "pkg-direct-dep.js"),
                    Path.Combine("contentFiles", "PackageLibraryDirectDependency.bundle.scp.css"),
                    Path.Combine("contentFiles", "wwwroot", "css", "site.css"),
                });
        }

        [Fact]
        public void Pack_DoesNotIncludeAnyCustomPropsFiles_WhenNoStaticAssetsAreAvailable()
        {
            var testAsset = "RazorComponentLibrary";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var pack = CreatePackCommand(projectDirectory);
            var result = ExecuteCommand(pack);

            var outputPath = pack.GetOutputDirectory("netstandard2.0", "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "ComponentLibrary.dll")).Should().Exist();

            result.Should().NuPkgDoesNotContain(
                Path.Combine(projectDirectory.Path, "bin", "Debug", "ComponentLibrary.1.0.0.nupkg"),
                filePaths: new[]
                {
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "ComponentLibrary.props"),
                    Path.Combine("buildMultiTargeting", "ComponentLibrary.props"),
                    Path.Combine("buildTransitive", "ComponentLibrary.props")
                });
        }

        [Fact]
        public void Pack_Incremental_DoesNotRegenerateCacheAndPropsFiles()
        {
            var testAsset = "PackageLibraryTransitiveDependency";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset, testAssetSubdirectory: "TestPackages")
                .WithSource();

            var pack = CreatePackCommand(projectDirectory);
            var result = ExecuteCommand(pack);

            var intermediateOutputPath = pack.GetIntermediateDirectory(DefaultTfm, "Debug").ToString();
            var outputPath = pack.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryTransitiveDependency.dll")).Should().Exist();

            new FileInfo(Path.Combine(intermediateOutputPath, "staticwebassets", "msbuild.PackageLibraryTransitiveDependency.Microsoft.AspNetCore.StaticWebAssets.props")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "staticwebassets", "msbuild.build.PackageLibraryTransitiveDependency.props")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "staticwebassets", "msbuild.buildMultiTargeting.PackageLibraryTransitiveDependency.props")).Should().Exist();
            new FileInfo(Path.Combine(intermediateOutputPath, "staticwebassets", "msbuild.buildTransitive.PackageLibraryTransitiveDependency.props")).Should().Exist();

            var directoryPath = Path.Combine(intermediateOutputPath, "staticwebassets");
            var thumbPrints = new Dictionary<string, FileThumbPrint>();
            var thumbPrintFiles = new[]
            {
                Path.Combine(directoryPath, "msbuild.PackageLibraryTransitiveDependency.Microsoft.AspNetCore.StaticWebAssets.props"),
                Path.Combine(directoryPath, "msbuild.build.PackageLibraryTransitiveDependency.props"),
                Path.Combine(directoryPath, "msbuild.buildMultiTargeting.PackageLibraryTransitiveDependency.props"),
                Path.Combine(directoryPath, "msbuild.buildTransitive.PackageLibraryTransitiveDependency.props"),
            };

            foreach (var file in thumbPrintFiles)
            {
                var thumbprint = FileThumbPrint.Create(file);
                thumbPrints[file] = thumbprint;
            }

            // Act
            var incremental = CreatePackCommand(projectDirectory);
            incremental.Execute().Should().Pass();
            foreach (var file in thumbPrintFiles)
            {
                var thumbprint = FileThumbPrint.Create(file);
                Assert.Equal(thumbPrints[file], thumbprint);
            }
        }

        [Fact]
        public void Build_StaticWebAssets_GeneratePackageOnBuild_PacksStaticWebAssets()
        {
            var testAsset = "PackageLibraryDirectDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            File.WriteAllText(Path.Combine(projectDirectory.Path, "PackageLibraryDirectDependency", "wwwroot", "LICENSE"), "license file contents");

            var buildCommand = CreateBuildCommand(projectDirectory, "PackageLibraryDirectDependency");
            var result = buildCommand.Execute("/p:GeneratePackageOnBuild=true");

            result.Should().Pass();

            var outputPath = buildCommand.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryDirectDependency.dll")).Should().Exist();

            result.Should().NuPkgContainsPatterns(
                Path.Combine(buildCommand.GetPackageDirectory().FullName, "PackageLibraryDirectDependency.1.0.0.nupkg"),
                filePatterns: new[]
                {
                    Path.Combine("staticwebassets", "js", "pkg-direct-dep.js"),
                    Path.Combine("staticwebassets", "css", "site.css"),
                    Path.Combine("staticwebassets", "PackageLibraryDirectDependency.*.bundle.scp.css"),
                    Path.Combine("build", "Microsoft.AspNetCore.StaticWebAssets.props"),
                    Path.Combine("build", "PackageLibraryDirectDependency.props"),
                    Path.Combine("buildMultiTargeting", "PackageLibraryDirectDependency.props"),
                    Path.Combine("buildTransitive", "PackageLibraryDirectDependency.props")
                });
        }

        [Fact]
        public void Build_StaticWebAssets_GeneratePackageOnBuild_DoesNotIncludeAssetsAsContent()
        {
            var testAsset = "PackageLibraryDirectDependency";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, subdirectory: "TestPackages");

            File.WriteAllText(Path.Combine(projectDirectory.Path, "PackageLibraryDirectDependency", "wwwroot", "LICENSE"), "license file contents");

            var buildCommand = CreateBuildCommand(projectDirectory, "PackageLibraryDirectDependency");
            var result = buildCommand.Execute("/p:GeneratePackageOnBuild=true");

            result.Should().Pass();

            var outputPath = buildCommand.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "PackageLibraryDirectDependency.dll")).Should().Exist();

            result.Should().NuPkgDoesNotContainPatterns(
                Path.Combine(buildCommand.GetPackageDirectory().FullName, "PackageLibraryDirectDependency.1.0.0.nupkg"),
                filePatterns: new[]
                {
                    Path.Combine("content", "js", "pkg-direct-dep.js"),
                    Path.Combine("content", "css", "site.css"),
                    Path.Combine("content", "PackageLibraryDirectDependency.*.bundle.scp.css"),
                    Path.Combine("contentFiles", "js", "pkg-direct-dep.js"),
                    Path.Combine("contentFiles", "css", "site.css"),
                    Path.Combine("contentFiles", "PackageLibraryDirectDependency.bundle.scp.css"),
                });
        }
    }
}
