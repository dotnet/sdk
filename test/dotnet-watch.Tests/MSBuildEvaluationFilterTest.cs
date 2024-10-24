// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Watcher.Internal;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    public class MSBuildEvaluationFilterTest
    {
        private static readonly EvaluationResult s_emptyEvaluationResult = new(new Dictionary<string, FileItem>(), projectGraph: null);

        [Fact]
        public async Task ProcessAsync_EvaluatesFileSetIfProjFileChanges()
        {
            var context = new DotNetWatchContext
            {
                Reporter = NullReporter.Singleton,
                Options = new(),
                RootProjectOptions = TestOptions.ProjectOptions,
                EnvironmentOptions = TestOptions.GetEnvironmentOptions()
            };

            var fileSetFactory = new MockFileSetFactory() { TryCreateImpl = () => s_emptyEvaluationResult };
            var evaluator = new BuildEvaluator(context, fileSetFactory);

            await evaluator.EvaluateAsync(changedFile: null, CancellationToken.None);

            evaluator.RequiresRevaluation = false;

            await evaluator.EvaluateAsync(changedFile: new(new() { FilePath = "Test.csproj" }, ChangeKind.Update), CancellationToken.None);

            Assert.True(evaluator.RequiresRevaluation);
        }

        [Fact]
        public async Task ProcessAsync_DoesNotEvaluateFileSetIfNonProjFileChanges()
        {
            var context = new DotNetWatchContext
            {
                Reporter = NullReporter.Singleton,
                Options = new(),
                RootProjectOptions = TestOptions.ProjectOptions,
                EnvironmentOptions = TestOptions.GetEnvironmentOptions()
            };

            var counter = 0;
            var fileSetFactory = new MockFileSetFactory() { TryCreateImpl = () => { counter++; return s_emptyEvaluationResult; } };
            var evaluator = new BuildEvaluator(context, fileSetFactory);

            await evaluator.EvaluateAsync(changedFile: null, CancellationToken.None);

            evaluator.RequiresRevaluation = false;

            await evaluator.EvaluateAsync(changedFile: new(new() { FilePath = "Controller.cs" }, ChangeKind.Update), CancellationToken.None);

            Assert.False(evaluator.RequiresRevaluation);
            Assert.Equal(1, counter);
        }

        [Fact]
        public async Task ProcessAsync_EvaluateFileSetOnEveryChangeIfOptimizationIsSuppressed()
        {
            var context = new DotNetWatchContext
            {
                Reporter = NullReporter.Singleton,
                Options = new(),
                RootProjectOptions = TestOptions.ProjectOptions,
                EnvironmentOptions = TestOptions.GetEnvironmentOptions() with { SuppressMSBuildIncrementalism = true }
            };

            var counter = 0;
            var fileSetFactory = new MockFileSetFactory() { TryCreateImpl = () => { counter++; return s_emptyEvaluationResult; } };

            var evaluator = new BuildEvaluator(context, fileSetFactory);

            await evaluator.EvaluateAsync(changedFile: null, CancellationToken.None);

            evaluator.RequiresRevaluation = false;

            await evaluator.EvaluateAsync(changedFile: new(new() { FilePath = "Controller.cs" }, ChangeKind.Update), CancellationToken.None);

            Assert.True(evaluator.RequiresRevaluation);
            Assert.Equal(2, counter);
        }

        [Fact]
        public async Task ProcessAsync_SetsEvaluationRequired_IfMSBuildFileChanges_ButIsNotChangedFile()
        {
            // There's a chance that the watcher does not correctly report edits to msbuild files on
            // concurrent edits. MSBuildEvaluationFilter uses timestamps to additionally track changes to these files.

            var result = new EvaluationResult(
                new Dictionary<string, FileItem>()
                {
                    { "Controlller.cs", new FileItem { FilePath = "Controlller.cs" } },
                    { "Proj.csproj", new FileItem { FilePath = "Proj.csproj" } },
                },
                projectGraph: null);

            var fileSetFactory = new MockFileSetFactory() { TryCreateImpl = () => result };

            var context = new DotNetWatchContext
            {
                Reporter = NullReporter.Singleton,
                Options = new(),
                RootProjectOptions = TestOptions.ProjectOptions,
                EnvironmentOptions = TestOptions.GetEnvironmentOptions(),
            };

            var evaluator = new TestableBuildEvaluator(context, fileSetFactory)
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

            await evaluator.EvaluateAsync(new(new() { FilePath = "Controller.cs" }, ChangeKind.Update), CancellationToken.None);

            Assert.True(evaluator.RequiresRevaluation);
        }

        private class TestableBuildEvaluator(DotNetWatchContext context, MSBuildFileSetFactory factory)
            : BuildEvaluator(context, factory)
        {
            public Dictionary<string, DateTime> Timestamps { get; } = [];
            private protected override DateTime GetLastWriteTimeUtcSafely(string file) => Timestamps[file];
        }
    }
}
