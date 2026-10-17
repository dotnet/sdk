// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.DotNet.Cli.Commands.New.PostActions;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.DotNet.Cli.New.Tests
{

    [TestClass]
    public class DotnetAddPostActionTests : SdkTest
    {
        private IEngineEnvironmentSettings _engineEnvironmentSettings = null!;

        [TestInitialize]
        public void TestInit()
        {
            _engineEnvironmentSettings = new EnvironmentSettingsHelper().CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true);
        }

        private static string TestCsprojFile
        {
            get
            {
                return @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>netcoreapp1.1</TargetFramework>
  </PropertyGroup>
</Project>";
            }
        }

        [TestMethod]
        public void AddRefFindsOneDefaultProjFileInOutputDirectory()
        {
            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            string projFileFullPath = Path.Combine(targetBasePath, "MyApp.proj");
            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(projFileFullPath, TestCsprojFile);

            DotnetAddPostActionProcessor actionProcessor = new();
            string outputBasePath = targetBasePath;

            IReadOnlyList<string> projFilesFound = DotnetAddPostActionProcessor.FindProjFileAtOrAbovePath(_engineEnvironmentSettings.Host.FileSystem, outputBasePath, new HashSet<string>());
            Assert.ContainsSingle(projFilesFound);
        }

        [TestMethod]
        public void AddRefFindsOneNameConfiguredProjFileInOutputDirectory()
        {
            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            string fooprojFileFullPath = Path.Combine(targetBasePath, "MyApp.fooproj");
            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(fooprojFileFullPath, TestCsprojFile);

            DotnetAddPostActionProcessor actionProcessor = new();
            string outputBasePath = targetBasePath;

            HashSet<string> projectFileExtensions = new() { ".fooproj" };
            IReadOnlyList<string> projFilesFound = DotnetAddPostActionProcessor.FindProjFileAtOrAbovePath(_engineEnvironmentSettings.Host.FileSystem, outputBasePath, projectFileExtensions);
            Assert.ContainsSingle(projFilesFound);
        }

        [TestMethod]
        public void AddRefFindsOneNameConfiguredProjFileWhenMultipleExtensionsAreAllowed()
        {
            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            string fooprojFileFullPath = Path.Combine(targetBasePath, "MyApp.fooproj");
            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(fooprojFileFullPath, TestCsprojFile);

            DotnetAddPostActionProcessor actionProcessor = new();
            string outputBasePath = targetBasePath;

            HashSet<string> projectFileExtensions = new() { ".fooproj", ".barproj" };
            IReadOnlyList<string> projFilesFound = DotnetAddPostActionProcessor.FindProjFileAtOrAbovePath(_engineEnvironmentSettings.Host.FileSystem, outputBasePath, projectFileExtensions);
            Assert.ContainsSingle(projFilesFound);
        }

        [TestMethod]
        public void AddRefIgnoresOtherProjectTypesWhenMultipleTypesAreAllowed()
        {
            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            string fooprojFileFullPath = Path.Combine(targetBasePath, "MyApp.fooproj");
            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(fooprojFileFullPath, TestCsprojFile);

            string barprojFileFullPath = Path.Combine(targetBasePath, "MyApp.barproj");
            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(barprojFileFullPath, TestCsprojFile);

            string csprojFileFullPath = Path.Combine(targetBasePath, "MyApp.csproj");
            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(csprojFileFullPath, TestCsprojFile);

            string fsprojFileFullPath = Path.Combine(targetBasePath, "MyApp.fsproj");
            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(fsprojFileFullPath, TestCsprojFile);

            DotnetAddPostActionProcessor actionProcessor = new();
            string outputBasePath = targetBasePath;

            HashSet<string> projectFileExtensions = new() { ".bazproj", ".fsproj" };
            IReadOnlyList<string> projFilesFound = DotnetAddPostActionProcessor.FindProjFileAtOrAbovePath(_engineEnvironmentSettings.Host.FileSystem, outputBasePath, projectFileExtensions);
            Assert.ContainsSingle(projFilesFound);
        }

        [TestMethod]
        public void AddRefFindsOneDefaultProjFileInAncestorOfOutputDirectory()
        {
            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            string projFileFullPath = Path.Combine(targetBasePath, "MyApp.xproj");
            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(projFileFullPath, TestCsprojFile);

            string outputBasePath = Path.Combine(targetBasePath, "ChildDir", "GrandchildDir");
            _engineEnvironmentSettings.Host.FileSystem.CreateDirectory(outputBasePath);

            DotnetAddPostActionProcessor actionProcessor = new();
            IReadOnlyList<string> projFilesFound = DotnetAddPostActionProcessor.FindProjFileAtOrAbovePath(_engineEnvironmentSettings.Host.FileSystem, outputBasePath, new HashSet<string>());
            Assert.ContainsSingle(projFilesFound);
        }

        [TestMethod]
        public void AddRefFindsMultipleDefaultProjFilesInOutputDirectory()
        {
            string projFilesOriginalContent = TestCsprojFile;
            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            string projFileFullPathOne = Path.Combine(targetBasePath, "MyApp.anysproj");
            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(projFileFullPathOne, projFilesOriginalContent);

            string projFileFullPathTwo = Path.Combine(targetBasePath, "MyApp2.someproj");
            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(projFileFullPathTwo, projFilesOriginalContent);

            DotnetAddPostActionProcessor actionProcessor = new();
            string outputBasePath = targetBasePath;
            IReadOnlyList<string> projFilesFound = DotnetAddPostActionProcessor.FindProjFileAtOrAbovePath(_engineEnvironmentSettings.Host.FileSystem, outputBasePath, new HashSet<string>());
            Assert.HasCount(2, projFilesFound);
        }

        [TestMethod]
        public void AddRefFindsMultipleDefaultProjFilesInAncestorOfOutputDirectory()
        {
            string projFilesOriginalContent = TestCsprojFile;
            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            string projFileFullPathOne = Path.Combine(targetBasePath, "MyApp.fooproj");
            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(projFileFullPathOne, projFilesOriginalContent);

            string projFileFullPathTwo = Path.Combine(targetBasePath, "MyApp2.barproj");
            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(projFileFullPathTwo, projFilesOriginalContent);

            string outputBasePath = Path.Combine(targetBasePath, "ChildDir", "GrandchildDir");

            DotnetAddPostActionProcessor actionProcessor = new();
            IReadOnlyList<string> projFilesFound = DotnetAddPostActionProcessor.FindProjFileAtOrAbovePath(_engineEnvironmentSettings.Host.FileSystem, outputBasePath, new HashSet<string>());
            Assert.HasCount(2, projFilesFound);
        }

        [TestMethod]
        public void AddRefCanHandleProjectFileRenames()
        {
            var callback = new MockAddProjectReferenceCallback();
            DotnetAddPostActionProcessor actionProcessor = new(callback.AddPackageReference, callback.AddProjectReference);

            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            string projFileFullPath = Path.Combine(targetBasePath, "MyApp.csproj");
            string referencedProjFileFullPath = Path.Combine(targetBasePath, "NewName.csproj");

            var args = new Dictionary<string, string>() { { "targetFiles", "[\"MyApp.csproj\"]" }, { "referenceType", "project" }, { "reference", "./OldName.csproj" } };
            var postAction = new MockPostAction(default, default, default, default, default!) { ActionId = DotnetAddPostActionProcessor.ActionProcessorId, Args = args };

            MockCreationEffects creationEffects = new MockCreationEffects()
                .WithFileChange(new MockFileChange("./OldName.csproj", "./NewName.csproj", ChangeKind.Create))
                .WithFileChange(new MockFileChange("./MyApp.csproj", "./MyApp.csproj", ChangeKind.Create));

            actionProcessor.Process(
                _engineEnvironmentSettings,
                postAction,
                creationEffects,
                new MockCreationResult(),
                targetBasePath);

            Assert.AreEqual(projFileFullPath, callback.Target);
            Assert.AreEqual(referencedProjFileFullPath, callback.Reference);
        }

        [TestMethod]
        public void AddRefCanHandleProjectFilesWithoutRenames()
        {
            var callback = new MockAddProjectReferenceCallback();
            DotnetAddPostActionProcessor actionProcessor = new(callback.AddPackageReference, callback.AddProjectReference);

            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            string projFileFullPath = Path.Combine(targetBasePath, "MyApp.csproj");
            string referencedProjFileFullPath = Path.Combine(targetBasePath, "Reference.csproj");

            var args = new Dictionary<string, string>() { { "targetFiles", "[\"MyApp.csproj\"]" }, { "referenceType", "project" }, { "reference", "./Reference.csproj" } };
            var postAction = new MockPostAction(default, default, default, default, default!) { ActionId = DotnetAddPostActionProcessor.ActionProcessorId, Args = args };

            MockCreationEffects creationEffects = new MockCreationEffects()
                .WithFileChange(new MockFileChange("./MyApp.csproj", "./MyApp.csproj", ChangeKind.Create));

            actionProcessor.Process(
                _engineEnvironmentSettings,
                postAction,
                creationEffects,
                new MockCreationResult(),
                targetBasePath);

            Assert.AreEqual(projFileFullPath, callback.Target);
            Assert.AreEqual(referencedProjFileFullPath, callback.Reference);
        }

        [TestMethod]
        public void AddRefCanHandleExistingProjectFiles()
        {
            var callback = new MockAddProjectReferenceCallback();
            DotnetAddPostActionProcessor actionProcessor = new(callback.AddPackageReference, callback.AddProjectReference);
             string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath(); //Commented code throws exception
            _engineEnvironmentSettings.Host.VirtualizeDirectory(targetBasePath);

            const string existingProjectFolder = "ExistingProjectFolder";
            string existingProjectPath = Path.Combine(targetBasePath, existingProjectFolder);

            const string existingProjectFile = "ExistingProject.csproj";
            string existingProjectFileFullPath = Path.Combine(existingProjectPath, existingProjectFile);
            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(existingProjectFileFullPath, TestCsprojFile);

            string referencedProjectFileFullPath = Path.Combine(targetBasePath, "Reference.csproj");

            //TODO: Add test for both target files passed as array and string
            var args = new Dictionary<string, string>()
            {
                { "targetFiles", $"[\"{existingProjectFolder}/{existingProjectFile}\"]" },
                { "referenceType", "project" },
                { "reference", "Reference.csproj" }
            };
            var postAction =
                new MockPostAction(default, default, default, default, default!)
                {
                    ActionId = DotnetAddPostActionProcessor.ActionProcessorId, Args = args
                };

            MockCreationEffects creationEffects = new MockCreationEffects();

            actionProcessor.Process(
                _engineEnvironmentSettings,
                postAction,
                creationEffects,
                new MockCreationResult(),
                targetBasePath);

            Assert.AreEqual(existingProjectFileFullPath, callback.Target);
            Assert.AreEqual(referencedProjectFileFullPath, callback.Reference);
        }

        [TestMethod]
        public void AddRefCanTargetASingleProjectWithAJsonArray()
        {
            var callback = new MockAddProjectReferenceCallback();
            DotnetAddPostActionProcessor actionProcessor = new(callback.AddPackageReference, callback.AddProjectReference);

            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            string projFileFullPath = Path.Combine(targetBasePath, "MyApp.csproj");

            var args = new Dictionary<string, string>() { { "targetFiles", "[\"MyApp.csproj\"]" }, { "referenceType", "package" }, { "reference", "System.Net.Json" } };
            var postAction = new MockPostAction(default, default, default, default, default!) { ActionId = DotnetAddPostActionProcessor.ActionProcessorId, Args = args };

            MockCreationEffects creationEffects = new MockCreationEffects()
                .WithFileChange(new MockFileChange("./MyApp.csproj", "./MyApp.csproj", ChangeKind.Create));

            actionProcessor.Process(
                _engineEnvironmentSettings,
                postAction,
                creationEffects,
                new MockCreationResult(),
                targetBasePath);

            Assert.AreEqual(projFileFullPath, callback.Target);
            Assert.AreEqual("System.Net.Json", callback.Reference);
        }

        [TestMethod]
        public void AddRefCanTargetASingleProjectWithTheProjectName()
        {
            var callback = new MockAddProjectReferenceCallback();
            DotnetAddPostActionProcessor actionProcessor = new(callback.AddPackageReference, callback.AddProjectReference);

            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            string projFileFullPath = Path.Combine(targetBasePath, "MyApp.csproj");

            var args = new Dictionary<string, string>() { { "targetFiles", "MyApp.csproj" }, { "referenceType", "package" }, { "reference", "System.Net.Json" } };
            var postAction = new MockPostAction(default, default, default, default, default!) { ActionId = DotnetAddPostActionProcessor.ActionProcessorId, Args = args };

            MockCreationEffects creationEffects = new MockCreationEffects()
                .WithFileChange(new MockFileChange("./MyApp.csproj", "./MyApp.csproj", ChangeKind.Create));


            actionProcessor.Process(
                _engineEnvironmentSettings,
                postAction,
                creationEffects,
                new MockCreationResult(),
                targetBasePath);

            Assert.AreEqual(projFileFullPath, callback.Target);
            Assert.AreEqual("System.Net.Json", callback.Reference);
        }

        [TestMethod]
        [DataRow(null, false, DisplayName = "no noRestore argument keeps the restore")]
        [DataRow("false", false, DisplayName = "noRestore false keeps the restore")]
        [DataRow("true", true, DisplayName = "noRestore true skips the restore")]
        public void AddPackageRefPassesNoRestoreOnlyWhenTheTemplateAsksForIt(string? noRestoreArg, bool expected)
        {
            var callback = new MockAddProjectReferenceCallback();
            DotnetAddPostActionProcessor actionProcessor = new(callback.AddPackageReference, callback.AddProjectReference);

            string targetBasePath = _engineEnvironmentSettings.GetTempVirtualizedPath();
            string projFileFullPath = Path.Combine(targetBasePath, "MyApp.csproj");
            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(projFileFullPath, string.Empty);

            var args = new Dictionary<string, string>()
            {
                { "targetFiles", "[\"MyApp.csproj\"]" }, { "referenceType", "package" }, { "reference", "System.Text.Json" }, { "version", "9.0.0" }
            };
            if (noRestoreArg is not null)
            {
                args.Add("noRestore", noRestoreArg);
            }

            var postAction = new MockPostAction(default, default, default, default, default!) { ActionId = DotnetAddPostActionProcessor.ActionProcessorId, Args = args };
            MockCreationEffects creationEffects = new MockCreationEffects()
                .WithFileChange(new MockFileChange("./MyApp.csproj", "./MyApp.csproj", ChangeKind.Create));

            actionProcessor.Process(_engineEnvironmentSettings, postAction, creationEffects, new MockCreationResult(), targetBasePath);

            Assert.AreEqual(expected, callback.NoRestore);
        }

        [TestMethod]
        public void AddPackageRefKeepsAnExistingCentralPackageVersion()
        {
            // The repository already pins this package centrally. Passing the template version
            // would rewrite that entry for every project in the repository.
            string? version = AddNUnitUnderCentralPackageManagement(
                CentralPackagesFile("""<PackageVersion Include="NUnit" Version="4.5.0" />"""));

            Assert.IsNull(version);
        }

        [TestMethod]
        public void AddPackageRefUsesTheTemplateVersionWhenTheCentralFileHasNoEntry()
        {
            // Central package management is on, but nothing pins NUnit yet, so the template
            // version is what creates the entry.
            string? version = AddNUnitUnderCentralPackageManagement(
                CentralPackagesFile("""<PackageVersion Include="Some.Other.Package" Version="1.0.0" />"""));

            Assert.AreEqual("4.6.1", version);
        }

        [TestMethod]
        public void AddPackageRefKeepsACentralPackageVersionInheritedFromAParentFile()
        {
            // The nearest Directory.Packages.props only imports its parent, as the file from
            // `dotnet new packagesprops --inherit` does, and the pin lives in the parent.
            string? version = AddNUnitUnderCentralPackageManagement(
                """
                <Project>
                  <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Packages.props', '$(MSBuildThisFileDirectory)../'))"
                          Condition="$([MSBuild]::GetPathOfFileAbove('Directory.Packages.props', '$(MSBuildThisFileDirectory)../')) != ''" />
                </Project>
                """,
                parentDirectoryPackagesProps: CentralPackagesFile("""<PackageVersion Include="NUnit" Version="4.5.0" />"""));

            Assert.IsNull(version);
        }

        [TestMethod]
        public void AddPackageRefUsesTheTemplateVersionWhenTheCentralEntryTargetsAnotherFramework()
        {
            // The entry does not apply to the generated project's framework. Omitting the version
            // here would make NuGet write Version="*" and the restore fail with NU1011.
            string? version = AddNUnitUnderCentralPackageManagement(
                CentralPackagesFile("""<PackageVersion Include="NUnit" Version="4.5.0" Condition="'$(TargetFramework)' == 'net472'" />"""));

            Assert.AreEqual("4.6.1", version);
        }

        [TestMethod]
        public void AddPackageRefKeepsACentralPackageVersionConditionedOnTheProjectFramework()
        {
            string? version = AddNUnitUnderCentralPackageManagement(
                CentralPackagesFile($"""<PackageVersion Include="NUnit" Version="4.5.0" Condition="'$(TargetFramework)' == '{ToolsetInfo.CurrentTargetFramework}'" />"""));

            Assert.IsNull(version);
        }

        private static string CentralPackagesFile(string packageVersionItem) => $"""
            <Project>
              <PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup>
              <ItemGroup>{packageVersionItem}</ItemGroup>
            </Project>
            """;

        /// <summary>
        /// Runs an NUnit 4.6.1 add-package post-action against a real project on disk, since the
        /// central version lookup evaluates the project with MSBuild, and returns the version
        /// that reached the callback.
        /// </summary>
        private string? AddNUnitUnderCentralPackageManagement(string directoryPackagesProps, string? parentDirectoryPackagesProps = null, [CallerMemberName] string testName = "")
        {
            string repositoryRoot = TestAssetsManager.CreateTestDirectory(testName).Path;
            string projectDirectory = repositoryRoot;
            if (parentDirectoryPackagesProps is not null)
            {
                File.WriteAllText(Path.Combine(repositoryRoot, "Directory.Packages.props"), parentDirectoryPackagesProps);
                projectDirectory = Directory.CreateDirectory(Path.Combine(repositoryRoot, "src")).FullName;
            }

            File.WriteAllText(Path.Combine(projectDirectory, "Directory.Packages.props"), directoryPackagesProps);
            File.WriteAllText(Path.Combine(projectDirectory, "MyApp.csproj"), $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework></PropertyGroup>
                </Project>
                """);

            var callback = new MockAddProjectReferenceCallback();
            DotnetAddPostActionProcessor actionProcessor = new(callback.AddPackageReference, callback.AddProjectReference);
            var args = new Dictionary<string, string>()
            {
                { "targetFiles", "[\"MyApp.csproj\"]" }, { "referenceType", "package" }, { "reference", "NUnit" }, { "version", "4.6.1" }
            };
            var postAction = new MockPostAction(default, default, default, default, default!) { ActionId = DotnetAddPostActionProcessor.ActionProcessorId, Args = args };
            MockCreationEffects creationEffects = new MockCreationEffects()
                .WithFileChange(new MockFileChange("./MyApp.csproj", "./MyApp.csproj", ChangeKind.Create));

            Assert.IsTrue(actionProcessor.Process(_engineEnvironmentSettings, postAction, creationEffects, new MockCreationResult(), projectDirectory));
            Assert.AreEqual("NUnit", callback.Reference);
            return callback.Version;
        }

        private class MockAddProjectReferenceCallback
        {
            public string? Target { get; private set; }

            public string? Reference { get; private set; }

            public bool AddProjectReference(string target, string reference)
            {
                if (Target != null)
                {
                    throw new Exception($"{nameof(AddProjectReference)} is called more than once.");
                }

                Target = target;
                Reference = reference;

                return true;
            }

            public string? Version { get; private set; }

            public bool? NoRestore { get; private set; }

            public bool AddPackageReference(string target, string reference, string? version, bool noRestore)
            {
                if (Target != null)
                {
                    throw new Exception($"{nameof(AddPackageReference)} is called more than once.");
                }

                Target = target;
                Reference = reference;
                Version = version;
                NoRestore = noRestore;

                return true;
            }
        }
    }
}
