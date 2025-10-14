// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Xml.Linq;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishWithoutConflicts : SdkTest
    {
        public GivenThatWeWantToPublishWithoutConflicts(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyFact]
        public void It_solves_conflicts_between_package_and_implicit_references()
        {
            // Test case from https://github.com/dotnet/sdk/issues/3904.
            // This dll is included in both the explicit package reference and Microsoft.NET.Build.Extensions. We prevent a double write in 
            // _ComputeResolvedCopyLocalPublishAssets by removing dlls duplicated between package references and implicitly expanded .NET references.
            var reference = "System.Runtime.InteropServices.RuntimeInformation";
            var targetFramework = "net462";
            var testProject = new TestProject()
            {
                Name = "ConflictingFilePublish",
                IsSdkProject = true,
                TargetFrameworks = targetFramework
            };
            testProject.PackageReferences.Add(new TestPackageReference(reference, "4.3.0"));
            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.AspNetCore", "2.1.4"));
            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);

            var getValuesCommand = new GetValuesCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name), targetFramework, "ResolvedFileToPublish", GetValuesCommand.ValueType.Item)
            {
                DependsOnTargets = "Publish"
            };

            getValuesCommand
                .Execute()
                .Should()
                .Pass();

            var files = getValuesCommand.GetValues()
                .Where(file => file.Contains(reference));
            files.Count().Should().Be(1);

            // We should choose the system.runtime.interopservices.runtimeinformation file from Microsoft.NET.Build.Extensions as it has a higher AssemblyVersion (4.0.2.0 compared to 4.0.1.0)
            files.FirstOrDefault().Contains(@"Microsoft.NET.Build.Extensions\net462\lib\System.Runtime.InteropServices.RuntimeInformation.dll").Should().BeTrue();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_has_consistent_behavior_when_publishing_single_file(bool shouldPublishSingleFile)
        {
            var targetFramework = ToolsetInfo.CurrentTargetFramework;
            var testProject = new TestProject()
            {
                Name = "DuplicateFiles",
                TargetFrameworks = targetFramework,
                IsSdkProject = true,
                IsExe = true,
                RuntimeIdentifier = "win-x64",
                SelfContained = "true"
            };

            // The Microsoft.TestPlatform.CLI package contains System.Runtime.CompilerServices.Unsafe.dll as content, which could cause a double write with the same dll originating from the 
            // runtime package. Without _HandleFileConflictsForPublish this would be caught when by the bundler when publishing single file, but a normal publish would succeed with double writes.
            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.TestPlatform.CLI", "16.5.0"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: shouldPublishSingleFile.ToString());
            var getValuesCommand = new GetValuesCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name), targetFramework, "ResolvedFileToPublish", GetValuesCommand.ValueType.Item)
            {
                DependsOnTargets = "Publish"
            };

            if (shouldPublishSingleFile)
            {
                getValuesCommand.Execute("/p:PublishSingleFile=true")
                    .Should()
                    .Pass();
            }
            else
            {
                getValuesCommand.Execute()
                    .Should()
                    .Pass();

                var duplicatedDll = "System.Runtime.CompilerServices.Unsafe.dll";
                var files = getValuesCommand.GetValues()
                    .Where(file => file.Contains(duplicatedDll));
                files.Count().Should().Be(1);
                // We should choose the file from microsoft.netcore.app.runtime package over the Microsoft.TestPlatform.CLI package version
                files.FirstOrDefault().Contains("microsoft.netcore.app.runtime").Should().BeTrue();
            }
        }

        [Fact]
        public void It_does_not_error_on_duplicate_files_with_CopyToOutputDirectory_Never()
        {
            // Test case for https://github.com/dotnet/sdk/issues/XXXXX
            // Files with CopyToOutputDirectory="Never" should not trigger NETSDK1152 error
            // when both a parent project and a child project have the same file

            var targetFramework = ToolsetInfo.CurrentTargetFramework;

            // Create a library project with a None item that has CopyToOutputDirectory="Never"
            var libraryProject = new TestProject()
            {
                Name = "LibraryWithNoneCopyNever",
                TargetFrameworks = targetFramework,
                IsSdkProject = true,
            };
            libraryProject.SourceFiles.Add(".filenesting.json", "{}");
            libraryProject.SourceFiles.Add("Class1.cs", "namespace LibraryWithNoneCopyNever { public class Class1 {} }");
            libraryProject.AdditionalProperties.Add("EnableDefaultItems", "false");
            libraryProject.AdditionalProperties.Add("EnableDefaultCompileItems", "false");

            // Create an app project that references the library and also has the same file
            var appProject = new TestProject()
            {
                Name = "AppWithNoneCopyNever",
                TargetFrameworks = targetFramework,
                IsSdkProject = true,
                IsExe = true
            };
            appProject.SourceFiles.Add("Program.cs", "System.Console.WriteLine(\"Hello World\");");
            appProject.SourceFiles.Add(".filenesting.json", "{}");
            appProject.ReferencedProjects.Add(libraryProject);
            appProject.AdditionalProperties.Add("EnableDefaultItems", "false");
            appProject.AdditionalProperties.Add("EnableDefaultCompileItems", "false");

            var testAsset = _testAssetsManager.CreateTestProject(appProject);

            // Add the None items with CopyToOutputDirectory="Never" to both projects
            var libProjectFile = Path.Combine(testAsset.TestRoot, libraryProject.Name, $"{libraryProject.Name}.csproj");
            var libProjectXml = XDocument.Load(libProjectFile);
            var libItemGroup = new XElement("ItemGroup",
                new XElement("None",
                    new XAttribute("Include", ".filenesting.json"),
                    new XElement("CopyToOutputDirectory", "Never")),
                new XElement("Compile",
                    new XAttribute("Include", "**\\*.cs")));
            libProjectXml.Root.Add(libItemGroup);
            libProjectXml.Save(libProjectFile);

            var appProjectFile = Path.Combine(testAsset.TestRoot, appProject.Name, $"{appProject.Name}.csproj");
            var appProjectXml = XDocument.Load(appProjectFile);
            var appItemGroup = new XElement("ItemGroup",
                new XElement("None",
                    new XAttribute("Include", ".filenesting.json"),
                    new XElement("CopyToOutputDirectory", "Never")),
                new XElement("Compile",
                    new XAttribute("Include", "**\\*.cs")));
            appProjectXml.Root.Add(appItemGroup);
            appProjectXml.Save(appProjectFile);

            // Publishing should succeed without NETSDK1152 error
            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute()
                .Should()
                .Pass();

            // Verify that .filenesting.json is not in the publish output
            var publishDir = publishCommand.GetOutputDirectory(targetFramework).FullName;
            File.Exists(Path.Combine(publishDir, ".filenesting.json")).Should().BeFalse();
        }
    }
}
