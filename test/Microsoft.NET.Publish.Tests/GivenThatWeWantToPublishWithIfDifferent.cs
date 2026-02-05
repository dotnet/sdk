// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishWithIfDifferent : SdkTest
    {
        public GivenThatWeWantToPublishWithIfDifferent(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_publishes_content_files_with_IfDifferent_metadata()
        {
            var testProject = new TestProject()
            {
                Name = "PublishWithIfDifferent",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            // Add Program.cs to fix compilation
            testProject.SourceFiles["Program.cs"] = @"using System;
class Program { static void Main() => Console.WriteLine(""Hello""); }";
            
            // Add content files with different CopyToPublishDirectory metadata values
            testProject.SourceFiles["data1.txt"] = "Data file 1 content";
            testProject.SourceFiles["data2.txt"] = "Data file 2 content";
            testProject.SourceFiles["data3.txt"] = "Data file 3 content";

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            // Update the project file to set CopyToPublishDirectory metadata
            var projectFile = Path.Combine(testAsset.Path, testProject.Name, $"{testProject.Name}.csproj");
            var projectContent = File.ReadAllText(projectFile);
            projectContent = projectContent.Replace("</Project>", @"
  <ItemGroup>
    <Content Include=""data1.txt"" CopyToPublishDirectory=""IfDifferent"" />
    <Content Include=""data2.txt"" CopyToPublishDirectory=""Always"" />
    <Content Include=""data3.txt"" CopyToPublishDirectory=""PreserveNewest"" />
  </ItemGroup>
</Project>");
            File.WriteAllText(projectFile, projectContent);

            var publishCommand = new PublishCommand(testAsset);
            var publishResult = publishCommand.Execute();

            publishResult.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks);

            // Verify all content files are published
            publishDirectory.Should().HaveFile("data1.txt");
            publishDirectory.Should().HaveFile("data2.txt");
            publishDirectory.Should().HaveFile("data3.txt");

            // Verify file contents
            File.ReadAllText(Path.Combine(publishDirectory.FullName, "data1.txt")).Should().Be("Data file 1 content");
            File.ReadAllText(Path.Combine(publishDirectory.FullName, "data2.txt")).Should().Be("Data file 2 content");
            File.ReadAllText(Path.Combine(publishDirectory.FullName, "data3.txt")).Should().Be("Data file 3 content");
        }

        [Fact]
        public void It_skips_unchanged_files_with_IfDifferent_on_republish()
        {
            var testProject = new TestProject()
            {
                Name = "PublishIfDifferentSkipUnchanged",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            // Add Program.cs to fix compilation
            testProject.SourceFiles["Program.cs"] = @"using System;
class Program { static void Main() => Console.WriteLine(""Hello""); }";

            testProject.SourceFiles["unchangedData.txt"] = "Original content";
            testProject.SourceFiles["changedData.txt"] = "Original content";

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var projectFile = Path.Combine(testAsset.Path, testProject.Name, $"{testProject.Name}.csproj");
            var projectContent = File.ReadAllText(projectFile);
            projectContent = projectContent.Replace("</Project>", @"
  <ItemGroup>
    <Content Include=""unchangedData.txt"" CopyToPublishDirectory=""IfDifferent"" />
    <Content Include=""changedData.txt"" CopyToPublishDirectory=""IfDifferent"" />
  </ItemGroup>
</Project>");
            File.WriteAllText(projectFile, projectContent);

            // First publish
            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks);

            // Record timestamps after first publish
            var unchangedFileInfo = new FileInfo(Path.Combine(publishDirectory.FullName, "unchangedData.txt"));
            var changedFileInfo = new FileInfo(Path.Combine(publishDirectory.FullName, "changedData.txt"));
            
            unchangedFileInfo.Exists.Should().BeTrue();
            changedFileInfo.Exists.Should().BeTrue();

            var unchangedOriginalTime = unchangedFileInfo.LastWriteTimeUtc;
            var changedOriginalTime = changedFileInfo.LastWriteTimeUtc;

            // Wait to ensure timestamp difference would be detectable
            System.Threading.Thread.Sleep(1000);

            // Modify only one source file
            var changedSourcePath = Path.Combine(testAsset.Path, testProject.Name, "changedData.txt");
            File.WriteAllText(changedSourcePath, "Modified content");

            // Second publish
            publishCommand.Execute().Should().Pass();

            // Refresh file info
            unchangedFileInfo.Refresh();
            changedFileInfo.Refresh();

            // The unchanged file should have the same timestamp (wasn't copied)
            unchangedFileInfo.LastWriteTimeUtc.Should().Be(unchangedOriginalTime);

            // The changed file should have a new timestamp (was copied)
            changedFileInfo.LastWriteTimeUtc.Should().BeAfter(changedOriginalTime);

            // Verify content
            File.ReadAllText(Path.Combine(publishDirectory.FullName, "unchangedData.txt")).Should().Be("Original content");
            File.ReadAllText(Path.Combine(publishDirectory.FullName, "changedData.txt")).Should().Be("Modified content");
        }

        [Fact]
        public void It_handles_None_items_with_IfDifferent_metadata()
        {
            var testProject = new TestProject()
            {
                Name = "PublishNoneWithIfDifferent",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.SourceFiles["Program.cs"] = "class Program { static void Main() { } }";
            testProject.SourceFiles["config.json"] = "{ \"setting\": \"value\" }";

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var projectFile = Path.Combine(testAsset.Path, testProject.Name, $"{testProject.Name}.csproj");
            var projectContent = File.ReadAllText(projectFile);
            projectContent = projectContent.Replace("</Project>", @"
  <ItemGroup>
    <None Include=""config.json"" CopyToPublishDirectory=""IfDifferent"">
      <CopyToOutputDirectory>Never</CopyToOutputDirectory>
    </None>
  </ItemGroup>
</Project>");
            File.WriteAllText(projectFile, projectContent);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks);

            // Verify the None item with IfDifferent is published
            publishDirectory.Should().HaveFile("config.json");
            File.ReadAllText(Path.Combine(publishDirectory.FullName, "config.json")).Should().Be("{ \"setting\": \"value\" }");
        }

        [Fact]
        public void It_handles_Compile_items_with_IfDifferent_metadata()
        {
            var testProject = new TestProject()
            {
                Name = "PublishCompileWithIfDifferent",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.SourceFiles["Program.cs"] = "class Program { static void Main() { } }";
            testProject.SourceFiles["SourceFile.cs"] = @"
namespace PublishCompileWithIfDifferent
{
    public class SourceClass { }
}";

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var projectFile = Path.Combine(testAsset.Path, testProject.Name, $"{testProject.Name}.csproj");
            var projectContent = File.ReadAllText(projectFile);
            projectContent = projectContent.Replace("</Project>", @"
  <ItemGroup>
    <Compile Update=""SourceFile.cs"" CopyToPublishDirectory=""IfDifferent"" />
  </ItemGroup>
</Project>");
            File.WriteAllText(projectFile, projectContent);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks);

            // Verify the Compile item with IfDifferent is published
            publishDirectory.Should().HaveFile("SourceFile.cs");
        }

        [Fact]
        public void It_copies_IfDifferent_files_correctly_with_referenced_projects()
        {
            var referencedProject = new TestProject()
            {
                Name = "ReferencedProject",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            };

            referencedProject.SourceFiles["shared.txt"] = "Shared content from library";

            var mainProject = new TestProject()
            {
                Name = "MainProject",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
                ReferencedProjects = { referencedProject }
            };

            // Add Program.cs to fix compilation
            mainProject.SourceFiles["Program.cs"] = @"using System;
class Program { static void Main() => Console.WriteLine(""Hello""); }";
            
            mainProject.SourceFiles["main.txt"] = "Main project content";

            var testAsset = TestAssetsManager.CreateTestProject(mainProject);

            // Configure the referenced project to include the file with IfDifferent
            var referencedProjectFile = Path.Combine(testAsset.Path, referencedProject.Name, $"{referencedProject.Name}.csproj");
            var referencedProjectContent = File.ReadAllText(referencedProjectFile);
            referencedProjectContent = referencedProjectContent.Replace("</Project>", @"
  <ItemGroup>
    <Content Include=""shared.txt"" CopyToPublishDirectory=""IfDifferent"" />
  </ItemGroup>
</Project>");
            File.WriteAllText(referencedProjectFile, referencedProjectContent);

            // Configure the main project
            var mainProjectFile = Path.Combine(testAsset.Path, mainProject.Name, $"{mainProject.Name}.csproj");
            var mainProjectContent = File.ReadAllText(mainProjectFile);
            mainProjectContent = mainProjectContent.Replace("</Project>", @"
  <ItemGroup>
    <Content Include=""main.txt"" CopyToPublishDirectory=""IfDifferent"" />
  </ItemGroup>
</Project>");
            File.WriteAllText(mainProjectFile, mainProjectContent);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(mainProject.TargetFrameworks);

            // Verify files from both projects are published
            publishDirectory.Should().HaveFile("main.txt");
            publishDirectory.Should().HaveFile("shared.txt");

            File.ReadAllText(Path.Combine(publishDirectory.FullName, "main.txt")).Should().Be("Main project content");
            File.ReadAllText(Path.Combine(publishDirectory.FullName, "shared.txt")).Should().Be("Shared content from library");
        }

        [Fact]
        public void It_handles_mixed_CopyToPublishDirectory_metadata_values()
        {
            var testProject = new TestProject()
            {
                Name = "MixedCopyMetadata",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.SourceFiles["Program.cs"] = "class Program { static void Main() { } }";
            testProject.SourceFiles["always.txt"] = "Always copy";
            testProject.SourceFiles["preserveNewest.txt"] = "PreserveNewest copy";
            testProject.SourceFiles["ifDifferent.txt"] = "IfDifferent copy";
            testProject.SourceFiles["doNotCopy.txt"] = "Do not copy";

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var projectFile = Path.Combine(testAsset.Path, testProject.Name, $"{testProject.Name}.csproj");
            var projectContent = File.ReadAllText(projectFile);
            projectContent = projectContent.Replace("</Project>", @"
  <ItemGroup>
    <Content Include=""always.txt"" CopyToPublishDirectory=""Always"" />
    <Content Include=""preserveNewest.txt"" CopyToPublishDirectory=""PreserveNewest"" />
    <Content Include=""ifDifferent.txt"" CopyToPublishDirectory=""IfDifferent"" />
    <Content Include=""doNotCopy.txt"" CopyToPublishDirectory=""Never"" />
  </ItemGroup>
</Project>");
            File.WriteAllText(projectFile, projectContent);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks);

            // Verify the correct files are published
            publishDirectory.Should().HaveFile("always.txt");
            publishDirectory.Should().HaveFile("preserveNewest.txt");
            publishDirectory.Should().HaveFile("ifDifferent.txt");
            publishDirectory.Should().NotHaveFile("doNotCopy.txt");
        }

        [Fact]
        public void It_publishes_IfDifferent_files_with_TargetPath()
        {
            var testProject = new TestProject()
            {
                Name = "IfDifferentWithTargetPath",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.SourceFiles["Program.cs"] = "class Program { static void Main() { } }";
            testProject.SourceFiles[Path.Combine("source", "data.txt")] = "Data in subfolder";

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var projectFile = Path.Combine(testAsset.Path, testProject.Name, $"{testProject.Name}.csproj");
            var projectContent = File.ReadAllText(projectFile);
            projectContent = projectContent.Replace("</Project>", $@"
  <ItemGroup>
    <Content Include=""{Path.Combine("source", "data.txt")}"" CopyToPublishDirectory=""IfDifferent"">
      <TargetPath>{Path.Combine("output", "data.txt")}</TargetPath>
    </Content>
  </ItemGroup>
</Project>");
            File.WriteAllText(projectFile, projectContent);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks);

            // Verify the file is published to the target path
            var targetFile = Path.Combine(publishDirectory.FullName, "output", "data.txt");
            File.Exists(targetFile).Should().BeTrue();
            File.ReadAllText(targetFile).Should().Be("Data in subfolder");
        }

        [Fact]
        public void It_handles_IfDifferent_with_self_contained_publish()
        {
            var testProject = new TestProject()
            {
                Name = "IfDifferentSelfContained",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
                RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid(ToolsetInfo.CurrentTargetFramework)
            };

            testProject.AdditionalProperties["SelfContained"] = "true";
            testProject.SourceFiles["Program.cs"] = "class Program { static void Main() { } }";
            testProject.SourceFiles["appdata.txt"] = "Application data";

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var projectFile = Path.Combine(testAsset.Path, testProject.Name, $"{testProject.Name}.csproj");
            var projectContent = File.ReadAllText(projectFile);
            projectContent = projectContent.Replace("</Project>", @"
  <ItemGroup>
    <Content Include=""appdata.txt"" CopyToPublishDirectory=""IfDifferent"" />
  </ItemGroup>
</Project>");
            File.WriteAllText(projectFile, projectContent);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute().Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(
                testProject.TargetFrameworks,
                runtimeIdentifier: testProject.RuntimeIdentifier);

            // Verify the content file is published
            publishDirectory.Should().HaveFile("appdata.txt");
            File.ReadAllText(Path.Combine(publishDirectory.FullName, "appdata.txt")).Should().Be("Application data");
        }

        [Fact]
        public void It_publishes_content_from_imported_targets_with_correct_path()
        {
            // This test verifies that Content items introduced from imported .targets files
            // (where DefiningProjectDirectory differs from MSBuildProjectDirectory) are
            // published with the correct project-relative path and do not escape the publish directory.
            // 
            // The bug scenario: when a .targets file OUTSIDE the project directory adds a Content item,
            // using DefiningProjectDirectory to compute the relative path can result in paths with '..'
            // segments that escape the publish directory.

            var testProject = new TestProject()
            {
                Name = "PublishImportedContent",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.SourceFiles["Program.cs"] = "class Program { static void Main() { } }";

            var testAsset = TestAssetsManager.CreateTestProject(testProject);

            var projectDirectory = Path.Combine(testAsset.Path, testProject.Name);

            // Create the imported targets file OUTSIDE the project directory (sibling folder)
            // This is the key difference - DefiningProjectDirectory will be different from MSBuildProjectDirectory
            var externalImportsDir = Path.Combine(testAsset.Path, "ExternalImports");
            Directory.CreateDirectory(externalImportsDir);

            // Create the content file in the project directory (where we want it to be published from)
            var contentFile = Path.Combine(projectDirectory, "project-content.txt");
            File.WriteAllText(contentFile, "Content defined by external targets");

            // Create an imported .targets file OUTSIDE the project that adds a Content item
            // pointing to a file in the project directory. The issue is that DefiningProjectDirectory
            // will be ExternalImports/, not the project directory.
            var importedTargetsFile = Path.Combine(externalImportsDir, "ImportedContent.targets");
            File.WriteAllText(importedTargetsFile, $@"<Project>
  <ItemGroup>
    <!-- This Content item points to a file in the main project directory, but is defined in external .targets -->
    <Content Include=""$(MSBuildProjectDirectory)\project-content.txt"" CopyToPublishDirectory=""IfDifferent"" />
  </ItemGroup>
</Project>");

            // Update the main project file to import the external targets
            var projectFile = Path.Combine(projectDirectory, $"{testProject.Name}.csproj");
            var projectContent = File.ReadAllText(projectFile);
            projectContent = projectContent.Replace("</Project>", @"
  <Import Project=""..\ExternalImports\ImportedContent.targets"" />
</Project>");
            File.WriteAllText(projectFile, projectContent);

            var publishCommand = new PublishCommand(testAsset);
            var publishResult = publishCommand.Execute();

            publishResult.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks);

            // The content file should be published with a simple filename, not with path segments
            // that could escape the publish directory (e.g., "..\PublishImportedContent\project-content.txt")
            publishDirectory.Should().HaveFile("project-content.txt");

            // Verify the content is correct
            var publishedContentPath = Path.Combine(publishDirectory.FullName, "project-content.txt");
            File.ReadAllText(publishedContentPath).Should().Be("Content defined by external targets");

            // Ensure no files escaped to parent directories
            var parentDir = Directory.GetParent(publishDirectory.FullName);
            var potentialEscapedFiles = Directory.GetFiles(parentDir.FullName, "project-content.txt", SearchOption.AllDirectories)
                .Where(f => !f.StartsWith(publishDirectory.FullName));
            potentialEscapedFiles.Should().BeEmpty("Content file should not escape to directories outside publish folder");
        }
    }
}
