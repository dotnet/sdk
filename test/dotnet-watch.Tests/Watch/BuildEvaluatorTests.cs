// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests
{
    public partial class BuildEvaluatorTests
    {
        private static readonly EvaluationResult s_emptyEvaluationResult = new(new Dictionary<string, FileItem>(), projectGraph: null);

        private static DotNetWatchContext CreateContext(bool suppressMSBuildIncrementalism = false)
        {
            var environmentOptions = TestOptions.GetEnvironmentOptions() with
            {
                SuppressMSBuildIncrementalism = suppressMSBuildIncrementalism
            };

            return new DotNetWatchContext()
            {
                Reporter = NullReporter.Singleton,
                ProcessRunner = new ProcessRunner(environmentOptions.ProcessCleanupTimeout, CancellationToken.None),
                Options = new(),
                RootProjectOptions = TestOptions.ProjectOptions,
                EnvironmentOptions = environmentOptions
            };
        }

        [PlatformSpecificFact(TestPlatforms.Windows)] // "https://github.com/dotnet/sdk/issues/49307")
        public async Task ProcessAsync_EvaluatesFileSetIfProjFileChanges()
        {
            var context = CreateContext();

            var fileSetFactory = new MockFileSetFactory() { TryCreateImpl = () => s_emptyEvaluationResult };
            var evaluator = new TestBuildEvaluator(context, fileSetFactory);

            await evaluator.EvaluateAsync(changedFile: null, CancellationToken.None);

            evaluator.RequiresRevaluation = false;

            await evaluator.EvaluateAsync(changedFile: new(new() { FilePath = "Test.csproj", ContainingProjectPaths = [] }, ChangeKind.Update), CancellationToken.None);

            Assert.True(evaluator.RequiresRevaluation);
        }

        [PlatformSpecificFact(TestPlatforms.Windows)] // "https://github.com/dotnet/sdk/issues/49307")
        public async Task ProcessAsync_DoesNotEvaluateFileSetIfNonProjFileChanges()
        {
            var context = CreateContext();

            var counter = 0;
            var fileSetFactory = new MockFileSetFactory() { TryCreateImpl = () => { counter++; return s_emptyEvaluationResult; } };
            var evaluator = new TestBuildEvaluator(context, fileSetFactory);

            await evaluator.EvaluateAsync(changedFile: null, CancellationToken.None);

            evaluator.RequiresRevaluation = false;

            await evaluator.EvaluateAsync(changedFile: new(new() { FilePath = "Controller.cs", ContainingProjectPaths = [] }, ChangeKind.Update), CancellationToken.None);

            Assert.False(evaluator.RequiresRevaluation);
            Assert.Equal(1, counter);
        }

        [PlatformSpecificFact(TestPlatforms.Windows)] // "https://github.com/dotnet/sdk/issues/49307")
        public async Task ProcessAsync_EvaluateFileSetOnEveryChangeIfOptimizationIsSuppressed()
        {
            var context = CreateContext(suppressMSBuildIncrementalism: true);

            var counter = 0;
            var fileSetFactory = new MockFileSetFactory() { TryCreateImpl = () => { counter++; return s_emptyEvaluationResult; } };

            var evaluator = new TestBuildEvaluator(context, fileSetFactory);

            await evaluator.EvaluateAsync(changedFile: null, CancellationToken.None);

            evaluator.RequiresRevaluation = false;

            await evaluator.EvaluateAsync(changedFile: new(new() { FilePath = "Controller.cs", ContainingProjectPaths = [] }, ChangeKind.Update), CancellationToken.None);

            Assert.True(evaluator.RequiresRevaluation);
            Assert.Equal(2, counter);
        }

        [PlatformSpecificFact(TestPlatforms.Windows)] // "https://github.com/dotnet/sdk/issues/49307")
        public async Task ProcessAsync_SetsEvaluationRequired_IfMSBuildFileChanges_ButIsNotChangedFile()
        {
            // There's a chance that the watcher does not correctly report edits to msbuild files on
            // concurrent edits. MSBuildEvaluationFilter uses timestamps to additionally track changes to these files.

            var result = new EvaluationResult(
                new Dictionary<string, FileItem>()
                {
                    { "Controlller.cs", new FileItem { FilePath = "Controlller.cs", ContainingProjectPaths = []} },
                    { "Proj.csproj", new FileItem { FilePath = "Proj.csproj", ContainingProjectPaths = [] } },
                },
                projectGraph: null);

            var fileSetFactory = new MockFileSetFactory() { TryCreateImpl = () => result };

            var context = CreateContext();

            var evaluator = new TestBuildEvaluator(context, fileSetFactory)
            {
                Timestamps =
                {
                    ["Controller.cs"] = new DateTime(1000),
                    ["Proj.csproj"] = new DateTime(1000),
                }
            };

            await evaluator.EvaluateAsync(changedFile: null, CancellationToken.None);
            evaluator.RequiresRevaluation = false;
            evaluator.Timestamps["Proj.csproj"] = new DateTime(1007);

            await evaluator.EvaluateAsync(new(new() { FilePath = "Controller.cs", ContainingProjectPaths = [] }, ChangeKind.Update), CancellationToken.None);

            Assert.True(evaluator.RequiresRevaluation);
        }
    }
}
