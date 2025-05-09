// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.DotNet.Tools.New.PostActionProcessors;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.DotNet.Cli.New.Tests
{
    public class DotnetSlnPostActionTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;

        public DotnetSlnPostActionTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: false);
        }

        [Theory]
        [InlineData("MySln.sln")]
        [InlineData("MySln.slnx")]
        public void AddProjectToSolutionPostActionFindSolutionFileAtOutputPath(string solutionFileName)
        {
            string targetBasePath = GetTemporaryPath();
            string solutionFileFullPath = Path.Combine(targetBasePath, solutionFileName);
            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(solutionFileFullPath, string.Empty);

            IReadOnlyList<string> solutionFiles = DotnetSlnPostActionProcessor.FindSolutionFilesAtOrAbovePath(_engineEnvironmentSettings.Host.FileSystem, targetBasePath);
            Assert.Single(solutionFiles);
            Assert.Equal(solutionFileFullPath, solutionFiles[0]);
        }

        [Fact(DisplayName = nameof(AddProjectToSolutionPostActionFindsOneProjectToAdd))]
        public void AddProjectToSolutionPostActionFindsOneProjectToAdd()
        {
            string outputBasePath = GetTemporaryPath();
            IPostAction postAction = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = DotnetSlnPostActionProcessor.ActionProcessorId,
                Args = new Dictionary<string, string>()
                {
                    { "primaryOutputIndexes", "0" }
                }
            };

            ICreationResult creationResult = new MockCreationResult(primaryOutputs: new[] { new MockCreationPath(Path.GetFullPath("outputProj1.csproj")) });

            Assert.True(DotnetSlnPostActionProcessor.TryGetProjectFilesToAdd(postAction, creationResult, outputBasePath, out IReadOnlyList<string>? foundProjectFiles));
            Assert.Equal(1, foundProjectFiles?.Count);
            Assert.Equal(creationResult.PrimaryOutputs[0].Path, foundProjectFiles?[0]);
        }

        [Fact(DisplayName = nameof(AddProjectToSolutionPostActionFindsMultipleProjectsToAdd))]
        public void AddProjectToSolutionPostActionFindsMultipleProjectsToAdd()
        {
            string outputBasePath = GetTemporaryPath();
            IPostAction postAction = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = DotnetSlnPostActionProcessor.ActionProcessorId,
                Args = new Dictionary<string, string>()
                {
                    { "primaryOutputIndexes", "0; 2" }
                }
            };

            ICreationResult creationResult = new MockCreationResult(
                primaryOutputs: new[]
                {
                    new MockCreationPath(Path.GetFullPath("outputProj1.csproj")),
                    new MockCreationPath(Path.GetFullPath("dontFindMe.csproj")),
                    new MockCreationPath(Path.GetFullPath("outputProj2.csproj"))
                });

            Assert.True(DotnetSlnPostActionProcessor.TryGetProjectFilesToAdd(postAction, creationResult, outputBasePath, out IReadOnlyList<string>? foundProjectFiles));
            Assert.NotNull(foundProjectFiles);
            Assert.Equal(2, foundProjectFiles.Count);
            Assert.Contains(creationResult.PrimaryOutputs[0].Path, foundProjectFiles.ToList());
            Assert.Contains(creationResult.PrimaryOutputs[2].Path, foundProjectFiles.ToList());

            Assert.DoesNotContain(creationResult.PrimaryOutputs[1].Path, foundProjectFiles.ToList());
        }

        [Fact(DisplayName = nameof(AddProjectToSolutionPostActionDoesntFindProjectOutOfRange))]
        public void AddProjectToSolutionPostActionDoesntFindProjectOutOfRange()
        {
            IPostAction postAction = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = DotnetSlnPostActionProcessor.ActionProcessorId,
                Args = new Dictionary<string, string>()
                {
                    { "primaryOutputIndexes", "1" }
                }
            };

            ICreationResult creationResult = new MockCreationResult(primaryOutputs: new[] { new MockCreationPath("outputProj1.csproj") });

            Assert.False(DotnetSlnPostActionProcessor.TryGetProjectFilesToAdd(postAction, creationResult, string.Empty, out IReadOnlyList<string>? foundProjectFiles));
            Assert.Empty(foundProjectFiles);
        }

        [Fact(DisplayName = nameof(AddProjectToSolutionPostActionFindsMultipleProjectsToAddWithOutputBasePath))]
        public void AddProjectToSolutionPostActionFindsMultipleProjectsToAddWithOutputBasePath()
        {
            string outputBasePath = GetTemporaryPath();

            IPostAction postAction = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = DotnetSlnPostActionProcessor.ActionProcessorId,
                Args = new Dictionary<string, string>()
                {
                    { "primaryOutputIndexes", "0; 2" }
                }
            };

            ICreationResult creationResult = new MockCreationResult(
                primaryOutputs: new[]
                {
                    new MockCreationPath("outputProj1.csproj"),
                    new MockCreationPath("dontFindMe.csproj"),
                    new MockCreationPath("outputProj2.csproj")
                });
            string outputFileFullPath0 = Path.Combine(outputBasePath, creationResult.PrimaryOutputs[0].Path);
            string dontFindMeFullPath1 = Path.Combine(outputBasePath, creationResult.PrimaryOutputs[1].Path);
            string outputFileFullPath2 = Path.Combine(outputBasePath, creationResult.PrimaryOutputs[2].Path);

            Assert.True(DotnetSlnPostActionProcessor.TryGetProjectFilesToAdd(postAction, creationResult, outputBasePath, out IReadOnlyList<string>? foundProjectFiles));
            Assert.NotNull(foundProjectFiles);
            Assert.Equal(2, foundProjectFiles.Count);
            Assert.Contains(outputFileFullPath0, foundProjectFiles.ToList());
            Assert.Contains(outputFileFullPath2, foundProjectFiles.ToList());

            Assert.DoesNotContain(dontFindMeFullPath1, foundProjectFiles.ToList());
        }

        [Fact(DisplayName = nameof(AddProjectToSolutionPostActionWithoutPrimaryOutputIndexesWithOutputBasePath))]
        public void AddProjectToSolutionPostActionWithoutPrimaryOutputIndexesWithOutputBasePath()
        {
            string outputBasePath = GetTemporaryPath();

            IPostAction postAction = new MockPostAction(default, default, default, default, default!)
            {
                ActionId = DotnetSlnPostActionProcessor.ActionProcessorId,
                Args = new Dictionary<string, string>()
            };

            ICreationResult creationResult = new MockCreationResult(
                primaryOutputs: new[]
                {
                    new MockCreationPath("outputProj1.csproj"),
                    new MockCreationPath("outputProj2.csproj"),
                });
            string outputFileFullPath0 = Path.Combine(outputBasePath, creationResult.PrimaryOutputs[0].Path);
            string outputFileFullPath1 = Path.Combine(outputBasePath, creationResult.PrimaryOutputs[1].Path);

            Assert.True(DotnetSlnPostActionProcessor.TryGetProjectFilesToAdd(postAction, creationResult, outputBasePath, out IReadOnlyList<string>? foundProjectFiles));
            Assert.NotNull(foundProjectFiles);
            Assert.Equal(2, foundProjectFiles.Count);
            Assert.Contains(outputFileFullPath0, foundProjectFiles.ToList());
            Assert.Contains(outputFileFullPath1, foundProjectFiles.ToList());
        }

        [Theory]
        [InlineData("MySln.sln")]
        [InlineData("MySln.slnx")]
        public void AddProjectToSolutionCanTargetASingleProjectWithAJsonArray(string solutionFileName)
        {
            var callback = new MockAddProjectToSolutionCallback();
            var actionProcessor = new DotnetSlnPostActionProcessor(callback.AddProjectToSolution);

            string targetBasePath = GetTemporaryPath();
            string slnFileFullPath = Path.Combine(targetBasePath, solutionFileName);
            string projFileFullPath = Path.Combine(targetBasePath, "MyApp.csproj");

            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(slnFileFullPath, "");

            var args = new Dictionary<string, string>() { { "projectFiles", "[\"MyApp.csproj\"]" } };
            var postAction = new MockPostAction(default, default, default, default, default!) { ActionId = DotnetSlnPostActionProcessor.ActionProcessorId, Args = args };

            MockCreationEffects creationEffects = new MockCreationEffects()
                .WithFileChange(new MockFileChange("./MyApp.csproj", "./MyApp.csproj", ChangeKind.Create));

            actionProcessor.Process(
                _engineEnvironmentSettings,
                postAction,
                creationEffects,
                new MockCreationResult(),
                targetBasePath);

            Assert.Equal(new[] { projFileFullPath }, callback.Projects);
            Assert.Equal(slnFileFullPath, callback.Solution);
        }

        [Theory]
        [InlineData("MySln.sln")]
        [InlineData("MySln.slnx")]
        public void AddProjectToSolutionCanTargetASingleProjectWithTheProjectName(string solutionFileName)
        {
            var callback = new MockAddProjectToSolutionCallback();
            var actionProcessor = new DotnetSlnPostActionProcessor(callback.AddProjectToSolution);

            string targetBasePath = GetTemporaryPath();
            string slnFileFullPath = Path.Combine(targetBasePath, solutionFileName);
            string projFileFullPath = Path.Combine(targetBasePath, "MyApp.csproj");

            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(slnFileFullPath, "");

            var args = new Dictionary<string, string>() { { "projectFiles", "MyApp.csproj" } };
            var postAction = new MockPostAction(default, default, default, default, default!) { ActionId = DotnetSlnPostActionProcessor.ActionProcessorId, Args = args };

            MockCreationEffects creationEffects = new MockCreationEffects()
                .WithFileChange(new MockFileChange("./MyApp.csproj", "./MyApp.csproj", ChangeKind.Create));

            actionProcessor.Process(
                _engineEnvironmentSettings,
                postAction,
                creationEffects,
                new MockCreationResult(),
                targetBasePath);

            Assert.Equal(new[] { projFileFullPath }, callback.Projects);
            Assert.Equal(slnFileFullPath, callback.Solution);
        }

        [Theory]
        [InlineData("MySln.sln")]
        [InlineData("MySln.slnx")]
        public void AddProjectToSolutionCanPlaceProjectInSolutionRoot(string solutionFileName)
        {
            var callback = new MockAddProjectToSolutionCallback();
            var actionProcessor = new DotnetSlnPostActionProcessor(callback.AddProjectToSolution);

            string targetBasePath = GetTemporaryPath();
            string slnFileFullPath = Path.Combine(targetBasePath, solutionFileName);
            string projFileFullPath = Path.Combine(targetBasePath, "MyApp.csproj");

            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(slnFileFullPath, "");

            var args = new Dictionary<string, string>() {
                { "projectFiles", "MyApp.csproj" },
                { "inRoot", "true" }
            };
            var postAction = new MockPostAction(default, default, default, default, default!) { ActionId = DotnetSlnPostActionProcessor.ActionProcessorId, Args = args };

            MockCreationEffects creationEffects = new MockCreationEffects()
                .WithFileChange(new MockFileChange("./MyApp.csproj", "./MyApp.csproj", ChangeKind.Create));

            actionProcessor.Process(
                _engineEnvironmentSettings,
                postAction,
                creationEffects,
                new MockCreationResult(),
                targetBasePath);

            Assert.True(callback.InRoot);
            Assert.Null(callback.TargetFolder);
        }

        [Theory]
        [InlineData("MySln.sln")]
        [InlineData("MySln.slnx")]
        public void AddProjectToSolutionCanPlaceProjectInSolutionFolder(string solutionFileName)
        {
            var callback = new MockAddProjectToSolutionCallback();
            var actionProcessor = new DotnetSlnPostActionProcessor(callback.AddProjectToSolution);

            string targetBasePath = GetTemporaryPath();
            string slnFileFullPath = Path.Combine(targetBasePath, solutionFileName);
            string projFileFullPath = Path.Combine(targetBasePath, "MyApp.csproj");

            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(slnFileFullPath, "");

            var args = new Dictionary<string, string>() {
                { "projectFiles", "MyApp.csproj" },
                { "solutionFolder", "src" }
            };
            var postAction = new MockPostAction(default, default, default, default, default!) { ActionId = DotnetSlnPostActionProcessor.ActionProcessorId, Args = args };

            MockCreationEffects creationEffects = new MockCreationEffects()
                .WithFileChange(new MockFileChange("./MyApp.csproj", "./MyApp.csproj", ChangeKind.Create));

            actionProcessor.Process(
                _engineEnvironmentSettings,
                postAction,
                creationEffects,
                new MockCreationResult(),
                targetBasePath);

            Assert.Null(callback.InRoot);
            Assert.Equal("src", callback.TargetFolder);
        }

        [Theory]
        [InlineData("MySln.sln")]
        [InlineData("MySln.slnx")]
        public void AddProjectToSolutionFailsWhenSolutionFolderAndInRootSpecified(string solutionFileName)
        {
            var callback = new MockAddProjectToSolutionCallback();
            var actionProcessor = new DotnetSlnPostActionProcessor(callback.AddProjectToSolution);

            string targetBasePath = GetTemporaryPath();
            string slnFileFullPath = Path.Combine(targetBasePath, solutionFileName);
            string projFileFullPath = Path.Combine(targetBasePath, "MyApp.csproj");

            _engineEnvironmentSettings.Host.FileSystem.WriteAllText(slnFileFullPath, "");

            var args = new Dictionary<string, string>() {
                { "projectFiles", "MyApp.csproj" },
                { "inRoot", "true" },
                { "solutionFolder", "src" }
            };
            var postAction = new MockPostAction(default, default, default, default, default!) { ActionId = DotnetSlnPostActionProcessor.ActionProcessorId, Args = args };

            MockCreationEffects creationEffects = new MockCreationEffects()
                .WithFileChange(new MockFileChange("./MyApp.csproj", "./MyApp.csproj", ChangeKind.Create));

            bool result = actionProcessor.Process(
                _engineEnvironmentSettings,
                postAction,
                creationEffects,
                new MockCreationResult(),
                targetBasePath);

            Assert.False(result);
        }

        private string GetTemporaryPath()
        {
            string tempPath = Path.Combine(_engineEnvironmentSettings.Paths.GlobalSettingsDir, "sandbox", Guid.NewGuid().ToString()) + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(tempPath);
            return tempPath;
        }

        private class MockAddProjectToSolutionCallback
        {
            public string? Solution { get; private set; }

            public IReadOnlyList<string?>? Projects { get; private set; }

            public string? TargetFolder { get; private set; }

            public bool? InRoot { get; private set; }

            public bool AddProjectToSolution(string solution, IReadOnlyList<string?> projects, string? targetFolder, bool? inRoot)
            {
                Solution = solution;
                Projects = projects;
                InRoot = inRoot;
                TargetFolder = targetFolder;

                return true;
            }
        }
    }
}
