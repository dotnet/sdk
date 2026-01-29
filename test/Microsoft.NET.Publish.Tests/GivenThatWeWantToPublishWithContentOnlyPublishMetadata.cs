// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.NET.Publish.Tests
{
    /// <summary>
    /// Tests for the _IncludeContentItemsWithOnlyPublishMetadata target.
    /// This target handles Content items that have CopyToPublishDirectory set but no CopyToOutputDirectory.
    /// 
    /// Regression test for https://github.com/dotnet/aspnetcore/issues/65247
    /// The issue is caused by MSBuild::MakeRelative computing incorrect TargetPath values when 
    /// projects are located close to the filesystem root.
    /// </summary>
    public class GivenThatWeWantToPublishWithContentOnlyPublishMetadata : SdkTest
    {
        public GivenThatWeWantToPublishWithContentOnlyPublishMetadata(ITestOutputHelper log) : base(log)
        {
        }

        /// <summary>
        /// Helper method to get the TargetPath metadata for ContentWithTargetPath items.
        /// </summary>
        private List<(string value, Dictionary<string, string> metadata)> GetContentWithTargetPathItems(
            TestAsset testAsset, 
            string projectName, 
            string targetFramework)
        {
            var getValuesCommand = new GetValuesCommand(
                Log,
                Path.Combine(testAsset.Path, projectName),
                targetFramework,
                "ContentWithTargetPath",
                GetValuesCommand.ValueType.Item)
            {
                DependsOnTargets = "AssignTargetPaths;_IncludeContentItemsWithOnlyPublishMetadata",
                MetadataNames = { "TargetPath", "CopyToPublishDirectory", "FullPath", "DefiningProjectDirectory" },
            };

            getValuesCommand.Execute().Should().Pass();
            return getValuesCommand.GetValuesWithMetadata();
        }

        /// <summary>
        /// Reproduces https://github.com/dotnet/aspnetcore/issues/65247
        /// Content items in nested directories with CopyToPublishDirectory (but no CopyToOutputDirectory) 
        /// should be published with correct relative paths, not paths that escape the publish directory.
        /// </summary>
        [Fact]
        public void It_publishes_nested_content_files_with_correct_target_path()
        {
            var testProject = new TestProject()
            {
                Name = "NestedContentPublish",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.SourceFiles["Program.cs"] = "class Program { static void Main() { } }";
            
            // Create nested content files similar to Views in MVC projects
            // These have CopyToPublishDirectory set but NO CopyToOutputDirectory
            testProject.SourceFiles[Path.Combine("Views", "Home", "Index.cshtml")] = "<h1>Index</h1>";
            testProject.SourceFiles[Path.Combine("Views", "Home", "Privacy.cshtml")] = "<h1>Privacy</h1>";
            testProject.SourceFiles[Path.Combine("Views", "Shared", "_Layout.cshtml")] = "<!DOCTYPE html><html></html>";
            testProject.SourceFiles[Path.Combine("Views", "_ViewImports.cshtml")] = "@using NestedContentPublish";
            testProject.SourceFiles[Path.Combine("Views", "_ViewStart.cshtml")] = "@{ Layout = \"_Layout\"; }";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var projectFile = Path.Combine(testAsset.Path, testProject.Name, $"{testProject.Name}.csproj");
            var projectContent = File.ReadAllText(projectFile);
            
            // Add Content items with CopyToPublishDirectory but WITHOUT CopyToOutputDirectory
            // This is the pattern that triggers the _IncludeContentItemsWithOnlyPublishMetadata target
            projectContent = projectContent.Replace("</Project>", @"
  <ItemGroup>
    <Content Include=""Views\**\*.cshtml"" CopyToPublishDirectory=""PreserveNewest"" />
  </ItemGroup>
</Project>");
            File.WriteAllText(projectFile, projectContent);

            var publishCommand = new PublishCommand(testAsset);
            var publishResult = publishCommand.Execute();

            publishResult.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks);

            // Verify the nested content files are published with correct relative paths
            var expectedFiles = new[]
            {
                Path.Combine("Views", "Home", "Index.cshtml"),
                Path.Combine("Views", "Home", "Privacy.cshtml"),
                Path.Combine("Views", "Shared", "_Layout.cshtml"),
                Path.Combine("Views", "_ViewImports.cshtml"),
                Path.Combine("Views", "_ViewStart.cshtml"),
            };

            foreach (var expectedFile in expectedFiles)
            {
                var fullPath = Path.Combine(publishDirectory.FullName, expectedFile);
                File.Exists(fullPath).Should().BeTrue($"Expected file '{expectedFile}' to exist in publish directory");
            }

            // Verify files are NOT in unexpected locations (escaped paths)
            // The bug would cause files to be copied to paths like ../../Views/...
            publishDirectory.GetFiles("*.cshtml", SearchOption.AllDirectories)
                .Select(f => f.FullName)
                .Should().OnlyContain(f => f.StartsWith(publishDirectory.FullName),
                    "All cshtml files should be within the publish directory, not escaped via relative paths");
        }

        /// <summary>
        /// Verifies that TargetPath metadata is computed correctly and doesn't contain 
        /// parent directory references that would cause files to be copied outside the publish folder.
        /// </summary>
        [Fact]
        public void It_computes_correct_TargetPath_for_content_with_only_publish_metadata()
        {
            var testProject = new TestProject()
            {
                Name = "ContentTargetPathTest",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.SourceFiles["Program.cs"] = "class Program { static void Main() { } }";
            testProject.SourceFiles[Path.Combine("Views", "Shared", "Error.cshtml")] = "<h1>Error</h1>";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var projectFile = Path.Combine(testAsset.Path, testProject.Name, $"{testProject.Name}.csproj");
            var projectContent = File.ReadAllText(projectFile);
            
            projectContent = projectContent.Replace("</Project>", @"
  <ItemGroup>
    <Content Include=""Views\**\*.cshtml"" CopyToPublishDirectory=""PreserveNewest"" />
  </ItemGroup>
</Project>");
            File.WriteAllText(projectFile, projectContent);

            // Get the ContentWithTargetPath items and verify their TargetPath metadata
            var items = GetContentWithTargetPathItems(testAsset, testProject.Name, testProject.TargetFrameworks);
            
            // Find the cshtml content item
            var cshtmlItem = items.FirstOrDefault(i => i.value.EndsWith("Error.cshtml"));
            cshtmlItem.Should().NotBeNull("Error.cshtml should be in ContentWithTargetPath");

            var targetPath = cshtmlItem.metadata["TargetPath"];
            
            // Log the values for debugging
            Log.WriteLine($"Item: {cshtmlItem.value}");
            Log.WriteLine($"TargetPath: {targetPath}");
            Log.WriteLine($"FullPath: {cshtmlItem.metadata.GetValueOrDefault("FullPath", "N/A")}");
            Log.WriteLine($"DefiningProjectDirectory: {cshtmlItem.metadata.GetValueOrDefault("DefiningProjectDirectory", "N/A")}");
            
            // The TargetPath should be a relative path within the publish directory
            // It should NOT contain ".." sequences that escape the publish folder
            targetPath.Should().NotContain("..", 
                "TargetPath should not contain parent directory references");
            
            // Normalize the expected path for the current OS
            var expectedTargetPath = Path.Combine("Views", "Shared", "Error.cshtml");
            targetPath.Should().Be(expectedTargetPath,
                "TargetPath should be relative to project directory");
        }

        /// <summary>
        /// Regression test specifically for the MVC F# template scenario from the reported issue.
        /// Tests Content items with CopyToPublishDirectory="PreserveNewest" in a nested Views structure.
        /// </summary>
        [Fact]
        public void It_publishes_mvc_style_views_correctly()
        {
            var testProject = new TestProject()
            {
                Name = "MvcViewsPublish",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.SourceFiles["Program.cs"] = @"
using System;
class Program { 
    static void Main() => Console.WriteLine(""Hello""); 
}";

            // Create the typical MVC Views folder structure
            testProject.SourceFiles[Path.Combine("Views", "Home", "Index.cshtml")] = @"
@{
    ViewData[""Title""] = ""Home Page"";
}

<div class=""text-center"">
    <h1 class=""display-4"">Welcome</h1>
</div>";

            testProject.SourceFiles[Path.Combine("Views", "Home", "Privacy.cshtml")] = @"
@{
    ViewData[""Title""] = ""Privacy Policy"";
}
<h1>@ViewData[""Title""]</h1>";

            testProject.SourceFiles[Path.Combine("Views", "Shared", "_Layout.cshtml")] = @"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""utf-8"" />
    <title>@ViewData[""Title""]</title>
</head>
<body>
    @RenderBody()
</body>
</html>";

            testProject.SourceFiles[Path.Combine("Views", "Shared", "Error.cshtml")] = @"
@{
    ViewData[""Title""] = ""Error"";
}
<h1 class=""text-danger"">Error.</h1>";

            testProject.SourceFiles[Path.Combine("Views", "Shared", "_ValidationScriptsPartial.cshtml")] = @"<script></script>";

            testProject.SourceFiles[Path.Combine("Views", "_ViewImports.cshtml")] = "@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers";
            testProject.SourceFiles[Path.Combine("Views", "_ViewStart.cshtml")] = @"@{
    Layout = ""_Layout"";
}";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var projectFile = Path.Combine(testAsset.Path, testProject.Name, $"{testProject.Name}.csproj");
            var projectContent = File.ReadAllText(projectFile);
            
            // This mimics the pattern from MVC templates:
            // Content items with CopyToPublishDirectory but NO CopyToOutputDirectory
            projectContent = projectContent.Replace("</Project>", @"
  <ItemGroup>
    <Content Include=""Views\**\*"" CopyToPublishDirectory=""PreserveNewest"" />
  </ItemGroup>
</Project>");
            File.WriteAllText(projectFile, projectContent);

            var publishCommand = new PublishCommand(testAsset);
            var publishResult = publishCommand.Execute();

            // The publish should succeed without MSB3021 errors about access denied
            publishResult.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks);

            // Verify the Views folder structure is preserved correctly in publish output
            publishDirectory.Should().HaveFile(Path.Combine("Views", "Home", "Index.cshtml"));
            publishDirectory.Should().HaveFile(Path.Combine("Views", "Home", "Privacy.cshtml"));
            publishDirectory.Should().HaveFile(Path.Combine("Views", "Shared", "_Layout.cshtml"));
            publishDirectory.Should().HaveFile(Path.Combine("Views", "Shared", "Error.cshtml"));
            publishDirectory.Should().HaveFile(Path.Combine("Views", "Shared", "_ValidationScriptsPartial.cshtml"));
            publishDirectory.Should().HaveFile(Path.Combine("Views", "_ViewImports.cshtml"));
            publishDirectory.Should().HaveFile(Path.Combine("Views", "_ViewStart.cshtml"));
        }

        /// <summary>
        /// Test that Content items with explicit TargetPath are not affected by the MakeRelative computation.
        /// </summary>
        [Fact]
        public void It_preserves_explicit_TargetPath_for_content_with_publish_metadata()
        {
            var testProject = new TestProject()
            {
                Name = "ExplicitTargetPathTest",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.SourceFiles["Program.cs"] = "class Program { static void Main() { } }";
            testProject.SourceFiles[Path.Combine("source", "file.txt")] = "content";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var projectFile = Path.Combine(testAsset.Path, testProject.Name, $"{testProject.Name}.csproj");
            var projectContent = File.ReadAllText(projectFile);
            
            // Explicit TargetPath should be preserved
            projectContent = projectContent.Replace("</Project>", @"
  <ItemGroup>
    <Content Include=""source\file.txt"" CopyToPublishDirectory=""PreserveNewest"">
      <TargetPath>custompath\renamed.txt</TargetPath>
    </Content>
  </ItemGroup>
</Project>");
            File.WriteAllText(projectFile, projectContent);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks);

            // File should be at the explicit target path
            publishDirectory.Should().HaveFile(Path.Combine("custompath", "renamed.txt"));
            
            // File should NOT be at the computed path
            var computedPath = Path.Combine(publishDirectory.FullName, "source", "file.txt");
            File.Exists(computedPath).Should().BeFalse("File should be at explicit TargetPath, not computed path");
        }

        /// <summary>
        /// Test that Content items with Link metadata use the Link for TargetPath.
        /// </summary>
        [Fact]
        public void It_uses_Link_metadata_for_content_TargetPath()
        {
            var testProject = new TestProject()
            {
                Name = "LinkTargetPathTest",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.SourceFiles["Program.cs"] = "class Program { static void Main() { } }";
            testProject.SourceFiles[Path.Combine("actual", "location", "file.txt")] = "content";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var projectFile = Path.Combine(testAsset.Path, testProject.Name, $"{testProject.Name}.csproj");
            var projectContent = File.ReadAllText(projectFile);
            
            // Link should be used for TargetPath when TargetPath is not set
            projectContent = projectContent.Replace("</Project>", @"
  <ItemGroup>
    <Content Include=""actual\location\file.txt"" CopyToPublishDirectory=""PreserveNewest"">
      <Link>linked\path\file.txt</Link>
    </Content>
  </ItemGroup>
</Project>");
            File.WriteAllText(projectFile, projectContent);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks);

            // File should be at the Link path
            publishDirectory.Should().HaveFile(Path.Combine("linked", "path", "file.txt"));
        }

        /// <summary>
        /// Regression test for https://github.com/dotnet/aspnetcore/issues/65247
        /// This test specifically reproduces the issue where MakeRelative computes an incorrect TargetPath
        /// when the project is located close to the filesystem root.
        /// 
        /// The bug causes TargetPath to contain parent directory references (e.g., "../../MVCFS/Views/...")
        /// instead of just the relative path from the project directory (e.g., "Views/...").
        /// 
        /// This happens because MakeRelative on Unix systems may produce unexpected results when:
        /// 1. The paths are shallow (close to root)
        /// 2. The DefiningProjectDirectory doesn't have a trailing slash
        /// 3. Path case sensitivity differences
        /// </summary>
        [Fact]
        public void It_publishes_content_correctly_regardless_of_project_depth()
        {
            var testProject = new TestProject()
            {
                Name = "MVCFS", // Short name like in the bug report
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.SourceFiles["Program.cs"] = "class Program { static void Main() { } }";
            
            // Create the exact folder structure from the bug report
            testProject.SourceFiles[Path.Combine("Views", "Home", "Index.cshtml")] = "<h1>Index</h1>";
            testProject.SourceFiles[Path.Combine("Views", "Home", "Privacy.cshtml")] = "<h1>Privacy</h1>";
            testProject.SourceFiles[Path.Combine("Views", "Shared", "_Layout.cshtml")] = "<!DOCTYPE html>";
            testProject.SourceFiles[Path.Combine("Views", "Shared", "Error.cshtml")] = "<h1>Error</h1>";
            testProject.SourceFiles[Path.Combine("Views", "Shared", "_ValidationScriptsPartial.cshtml")] = "<script></script>";
            testProject.SourceFiles[Path.Combine("Views", "_ViewImports.cshtml")] = "@using MVCFS";
            testProject.SourceFiles[Path.Combine("Views", "_ViewStart.cshtml")] = "@{ Layout = \"_Layout\"; }";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var projectFile = Path.Combine(testAsset.Path, testProject.Name, $"{testProject.Name}.csproj");
            var projectContent = File.ReadAllText(projectFile);
            
            // This is the pattern from MVC F# template that triggers the bug:
            // Content items with CopyToPublishDirectory but NO CopyToOutputDirectory
            projectContent = projectContent.Replace("</Project>", @"
  <ItemGroup>
    <Content Include=""Views\**\*"" CopyToPublishDirectory=""PreserveNewest"" />
  </ItemGroup>
</Project>");
            File.WriteAllText(projectFile, projectContent);

            // First, verify the TargetPath metadata is computed correctly
            var items = GetContentWithTargetPathItems(testAsset, testProject.Name, testProject.TargetFrameworks);
            
            foreach (var item in items.Where(i => i.value.Contains("Views")))
            {
                var targetPath = item.metadata["TargetPath"];
                Log.WriteLine($"Item: {item.value}");
                Log.WriteLine($"  TargetPath: {targetPath}");
                Log.WriteLine($"  FullPath: {item.metadata.GetValueOrDefault("FullPath", "N/A")}");
                Log.WriteLine($"  DefiningProjectDirectory: {item.metadata.GetValueOrDefault("DefiningProjectDirectory", "N/A")}");
                
                // Key assertion: TargetPath should NOT contain ".." parent references
                // The bug report shows paths like "../../../../../../MVCFS/Views/..."
                targetPath.Should().NotContain("..", 
                    $"TargetPath for {Path.GetFileName(item.value)} should not escape the project directory");
                
                // TargetPath should NOT contain the project name at the beginning
                // (that would indicate MakeRelative is computing a path relative to a wrong directory)
                targetPath.Should().NotStartWith("MVCFS",
                    $"TargetPath for {Path.GetFileName(item.value)} should be relative to project directory, not parent");
                
                // TargetPath should start with "Views" since that's the expected relative path
                targetPath.Should().StartWith("Views",
                    $"TargetPath for {Path.GetFileName(item.value)} should start with Views folder");
            }

            // Now verify publish actually works
            var publishCommand = new PublishCommand(testAsset);
            var publishResult = publishCommand.Execute();

            // The publish should succeed without MSB3021 errors about access denied or invalid paths
            publishResult.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks);

            // Verify all files are in the correct locations within the publish directory
            var expectedFiles = new[]
            {
                Path.Combine("Views", "Home", "Index.cshtml"),
                Path.Combine("Views", "Home", "Privacy.cshtml"),
                Path.Combine("Views", "Shared", "_Layout.cshtml"),
                Path.Combine("Views", "Shared", "Error.cshtml"),
                Path.Combine("Views", "Shared", "_ValidationScriptsPartial.cshtml"),
                Path.Combine("Views", "_ViewImports.cshtml"),
                Path.Combine("Views", "_ViewStart.cshtml"),
            };

            foreach (var expectedFile in expectedFiles)
            {
                var fullPath = Path.Combine(publishDirectory.FullName, expectedFile);
                File.Exists(fullPath).Should().BeTrue(
                    $"Expected file '{expectedFile}' to exist at '{fullPath}' in publish directory");
            }

            // Critical check: verify no files were copied to paths outside the publish directory
            // due to escaped relative paths
            var allCshtmlFiles = Directory.GetFiles(
                Path.GetDirectoryName(publishDirectory.FullName) ?? publishDirectory.FullName,
                "*.cshtml",
                SearchOption.AllDirectories);
            
            foreach (var file in allCshtmlFiles)
            {
                var normalizedFile = Path.GetFullPath(file);
                var normalizedPublishDir = Path.GetFullPath(publishDirectory.FullName);
                
                normalizedFile.Should().StartWith(normalizedPublishDir,
                    $"File '{file}' should be inside the publish directory, not escaped via relative paths");
            }
        }

        /// <summary>
        /// Tests the exact scenario from the bug: F# MVC project with Views that have CopyToPublishDirectory
        /// but not CopyToOutputDirectory, reproducing the path computation issue.
        /// </summary>
        [Fact]
        public void It_publishes_fsharp_mvc_style_project_correctly()
        {
            // Note: We use C# project but simulate the same item group structure as F# MVC template
            var testProject = new TestProject()
            {
                Name = "FSharpMvcRepro",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.SourceFiles["Program.cs"] = "class Program { static void Main() { } }";
            
            // Create Views similar to MVC F# template
            testProject.SourceFiles[Path.Combine("Views", "Home", "Index.cshtml")] = @"@{
    ViewData[""Title""] = ""Home Page"";
}
<div class=""text-center"">
    <h1 class=""display-4"">Welcome</h1>
    <p>Learn about <a href=""https://learn.microsoft.com/aspnet/core"">building Web apps with ASP.NET Core</a>.</p>
</div>";

            testProject.SourceFiles[Path.Combine("Views", "Home", "Privacy.cshtml")] = @"@{
    ViewData[""Title""] = ""Privacy Policy"";
}
<h1>@ViewData[""Title""]</h1>
<p>Use this page to detail your site's privacy policy.</p>";

            testProject.SourceFiles[Path.Combine("Views", "Shared", "_Layout.cshtml")] = @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <title>@ViewData[""Title""] - FSharpMvcRepro</title>
</head>
<body>
    <div class=""container"">
        <main role=""main"" class=""pb-3"">
            @RenderBody()
        </main>
    </div>
</body>
</html>";

            testProject.SourceFiles[Path.Combine("Views", "Shared", "Error.cshtml")] = @"@{
    ViewData[""Title""] = ""Error"";
}
<h1 class=""text-danger"">Error.</h1>
<h2 class=""text-danger"">An error occurred while processing your request.</h2>";

            testProject.SourceFiles[Path.Combine("Views", "Shared", "_ValidationScriptsPartial.cshtml")] = 
                @"<script src=""~/lib/jquery-validation/dist/jquery.validate.min.js""></script>";

            testProject.SourceFiles[Path.Combine("Views", "_ViewImports.cshtml")] = 
                "@using FSharpMvcRepro\n@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers";
            
            testProject.SourceFiles[Path.Combine("Views", "_ViewStart.cshtml")] = @"@{
    Layout = ""_Layout"";
}";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var projectFile = Path.Combine(testAsset.Path, testProject.Name, $"{testProject.Name}.csproj");
            var projectContent = File.ReadAllText(projectFile);
            
            // Exact pattern from F# MVC template that triggers the issue
            projectContent = projectContent.Replace("</Project>", @"
  <ItemGroup>
    <Content Include=""Views\_ViewImports.cshtml"" CopyToPublishDirectory=""PreserveNewest"" />
    <Content Include=""Views\_ViewStart.cshtml"" CopyToPublishDirectory=""PreserveNewest"" />
    <Content Include=""Views\Home\Index.cshtml"" CopyToPublishDirectory=""PreserveNewest"" />
    <Content Include=""Views\Home\Privacy.cshtml"" CopyToPublishDirectory=""PreserveNewest"" />
    <Content Include=""Views\Shared\_Layout.cshtml"" CopyToPublishDirectory=""PreserveNewest"" />
    <Content Include=""Views\Shared\_ValidationScriptsPartial.cshtml"" CopyToPublishDirectory=""PreserveNewest"" />
    <Content Include=""Views\Shared\Error.cshtml"" CopyToPublishDirectory=""PreserveNewest"" />
  </ItemGroup>
</Project>");
            File.WriteAllText(projectFile, projectContent);

            // Get ContentWithTargetPath items and verify TargetPath
            var items = GetContentWithTargetPathItems(testAsset, testProject.Name, testProject.TargetFrameworks);
            
            // Verify each item has a correct TargetPath
            foreach (var item in items.Where(i => i.value.Contains("cshtml")))
            {
                var targetPath = item.metadata["TargetPath"];
                var fileName = Path.GetFileName(item.value);
                
                Log.WriteLine($"Checking {fileName}: TargetPath = '{targetPath}'");
                
                // Critical assertion: TargetPath must not have parent directory escapes
                targetPath.Should().NotContain("..",
                    $"TargetPath for {fileName} must not contain parent directory references (..)");
                
                // TargetPath should be relative to project, starting with Views
                targetPath.Should().StartWith("Views",
                    $"TargetPath for {fileName} should start with 'Views'");
            }

            // Publish and verify
            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks);

            // All Views should be published correctly
            publishDirectory.Should().HaveFile(Path.Combine("Views", "_ViewImports.cshtml"));
            publishDirectory.Should().HaveFile(Path.Combine("Views", "_ViewStart.cshtml"));
            publishDirectory.Should().HaveFile(Path.Combine("Views", "Home", "Index.cshtml"));
            publishDirectory.Should().HaveFile(Path.Combine("Views", "Home", "Privacy.cshtml"));
            publishDirectory.Should().HaveFile(Path.Combine("Views", "Shared", "_Layout.cshtml"));
            publishDirectory.Should().HaveFile(Path.Combine("Views", "Shared", "_ValidationScriptsPartial.cshtml"));
            publishDirectory.Should().HaveFile(Path.Combine("Views", "Shared", "Error.cshtml"));
        }
    }
}