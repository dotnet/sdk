// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.New.PostActions;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.DotNet.Cli.New.Tests
{

    [TestClass]
    public class DotnetAddPostActionTests
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

            public bool AddPackageReference(string target, string reference, string? version)
            {
                if (Target != null)
                {
                    throw new Exception($"{nameof(AddPackageReference)} is called more than once.");
                }

                Target = target;
                Reference = reference;

                return true;
            }
        }
    }
}
