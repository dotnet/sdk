// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Tools.Internal;
using Moq;

namespace Microsoft.DotNet.Watcher.Tools
{
    public class MSBuildEvaluationFilterTest
    {
        private static readonly FileSet s_emptyFileSet = new([]);

        [Fact]
        public async Task ProcessAsync_EvaluatesFileSetIfProjFileChanges()
        {
            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new(),
                Options = TestOptions.CommandLine,
                EnvironmentOptions = TestOptions.Environmental
            };

            var fileSetFactory = new MockFileSetFactory() { CreateImpl = _ => (null, s_emptyFileSet) };
            var filter = new MSBuildEvaluationFilter(context, fileSetFactory);

            var state = new WatchState()
            {
                Iteration = 0,
                ProcessSpec = new ProcessSpec()
            };

            await filter.EvaluateAsync(state, CancellationToken.None);

            state.Iteration++;
            state.ChangedFile = new FileItem { FilePath = "Test.csproj" };
            state.RequiresMSBuildRevaluation = false;

            await filter.EvaluateAsync(state, CancellationToken.None);

            Assert.True(state.RequiresMSBuildRevaluation);
        }

        [Fact]
        public async Task ProcessAsync_DoesNotEvaluateFileSetIfNonProjFileChanges()
        {
            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new(),
                Options = TestOptions.CommandLine,
                EnvironmentOptions = TestOptions.Environmental
            };

            var counter = 0;
            var fileSetFactory = new MockFileSetFactory() { CreateImpl = _ => { counter++; return (null, s_emptyFileSet); } };
            var filter = new MSBuildEvaluationFilter(context, fileSetFactory);

            var state = new WatchState()
            {
                Iteration = 0,
                ProcessSpec = new ProcessSpec()
            };

            await filter.EvaluateAsync(state, CancellationToken.None);

            state.Iteration++;
            state.ChangedFile = new FileItem { FilePath = "Controller.cs" };
            state.RequiresMSBuildRevaluation = false;

            await filter.EvaluateAsync(state, CancellationToken.None);

            Assert.False(state.RequiresMSBuildRevaluation);
            Assert.Equal(1, counter);
        }

        [Fact]
        public async Task ProcessAsync_EvaluateFileSetOnEveryChangeIfOptimizationIsSuppressed()
        {
            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new(),
                Options = TestOptions.CommandLine,
                EnvironmentOptions = TestOptions.Environmental with { SuppressMSBuildIncrementalism = true }
            };

            var counter = 0;
            var fileSetFactory = new MockFileSetFactory() { CreateImpl = _ => { counter++; return (null, s_emptyFileSet); } };

            var filter = new MSBuildEvaluationFilter(context, fileSetFactory);

            var state = new WatchState()
            {
                Iteration = 0,
                ProcessSpec = new ProcessSpec()
            };

            await filter.EvaluateAsync(state, CancellationToken.None);

            state.Iteration++;
            state.ChangedFile = new FileItem { FilePath = "Controller.cs" };
            state.RequiresMSBuildRevaluation = false;

            await filter.EvaluateAsync(state, CancellationToken.None);

            Assert.True(state.RequiresMSBuildRevaluation);
            Assert.Equal(2, counter);
        }

        [Fact]
        public async Task ProcessAsync_SetsEvaluationRequired_IfMSBuildFileChanges_ButIsNotChangedFile()
        {
            // There's a chance that the watcher does not correctly report edits to msbuild files on
            // concurrent edits. MSBuildEvaluationFilter uses timestamps to additionally track changes to these files.

            var fileSet = new FileSet([new FileItem { FilePath = "Controlller.cs" }, new FileItem { FilePath = "Proj.csproj" }]);
            var fileSetFactory = new MockFileSetFactory() { CreateImpl = _ => (null, fileSet) };

            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new(),
                Options = TestOptions.CommandLine,
                EnvironmentOptions = TestOptions.Environmental,
            };

            var filter = new TestableMSBuildEvaluationFilter(context, fileSetFactory)
            {
                Timestamps =
                {
                    ["Controller.cs"] = new DateTime(1000),
                    ["Proj.csproj"] = new DateTime(1000),
                }
            };

            var state = new WatchState()
            {
                Iteration = 0,
                ProcessSpec = new ProcessSpec()
            };

            await filter.EvaluateAsync(state, CancellationToken.None);
            state.RequiresMSBuildRevaluation = false;
            state.ChangedFile = new FileItem { FilePath = "Controller.cs" };
            state.Iteration++;
            filter.Timestamps["Proj.csproj"] = new DateTime(1007);

            await filter.EvaluateAsync(state, CancellationToken.None);

            Assert.True(state.RequiresMSBuildRevaluation);
        }

        private class TestableMSBuildEvaluationFilter(DotNetWatchContext context, FileSetFactory factory)
            : MSBuildEvaluationFilter(context, factory)
        {
            public Dictionary<string, DateTime> Timestamps { get; } = [];
            private protected override DateTime GetLastWriteTimeUtcSafely(string file) => Timestamps[file];
        }
    }
}
