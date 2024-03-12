// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Tools.Internal;
using Moq;

namespace Microsoft.DotNet.Watcher.Tools
{
    public class MSBuildEvaluationFilterTest
    {
        private static readonly FileSet s_emptyFileSet = new(projectInfo: null!, Array.Empty<FileItem>());

        private readonly IFileSetFactory _fileSetFactory = Mock.Of<IFileSetFactory>(
            f => f.CreateAsync(It.IsAny<CancellationToken>()) == Task.FromResult(s_emptyFileSet));

        [Fact]
        public async Task ProcessAsync_EvaluatesFileSetIfProjFileChanges()
        {
            // Arrange
            var filter = new MSBuildEvaluationFilter(_fileSetFactory);
            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new()
            };

            var state = new WatchState()
            {
                Iteration = 0,
                ProcessSpec = new ProcessSpec()
            };

            await filter.ProcessAsync(context, state, CancellationToken.None);

            state.Iteration++;
            state.ChangedFile = new FileItem { FilePath = "Test.csproj" };
            state.RequiresMSBuildRevaluation = false;

            // Act
            await filter.ProcessAsync(context, state, CancellationToken.None);

            // Assert
            Assert.True(state.RequiresMSBuildRevaluation);
        }

        [Fact]
        public async Task ProcessAsync_DoesNotEvaluateFileSetIfNonProjFileChanges()
        {
            // Arrange
            var filter = new MSBuildEvaluationFilter(_fileSetFactory);
            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new()
            };

            var state = new WatchState()
            {
                Iteration = 0,
                ProcessSpec = new ProcessSpec()
            };

            await filter.ProcessAsync(context, state, CancellationToken.None);

            state.Iteration++;
            state.ChangedFile = new FileItem { FilePath = "Controller.cs" };
            state.RequiresMSBuildRevaluation = false;

            // Act
            await filter.ProcessAsync(context, state, CancellationToken.None);

            // Assert
            Assert.False(state.RequiresMSBuildRevaluation);
            Mock.Get(_fileSetFactory).Verify(v => v.CreateAsync(It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task ProcessAsync_EvaluateFileSetOnEveryChangeIfOptimizationIsSuppressed()
        {
            // Arrange
            var filter = new MSBuildEvaluationFilter(_fileSetFactory);
            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                SuppressMSBuildIncrementalism = true,
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new()
            };

            var state = new WatchState()
            {
                Iteration = 0,
                ProcessSpec = new ProcessSpec()
            };

            await filter.ProcessAsync(context, state, CancellationToken.None);

            state.Iteration++;
            state.ChangedFile = new FileItem { FilePath = "Controller.cs" };
            state.RequiresMSBuildRevaluation = false;

            // Act
            await filter.ProcessAsync(context, state, CancellationToken.None);

            // Assert
            Assert.True(state.RequiresMSBuildRevaluation);
            Mock.Get(_fileSetFactory).Verify(v => v.CreateAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task ProcessAsync_SetsEvaluationRequired_IfMSBuildFileChanges_ButIsNotChangedFile()
        {
            // There's a chance that the watcher does not correctly report edits to msbuild files on
            // concurrent edits. MSBuildEvaluationFilter uses timestamps to additionally track changes to these files.

            // Arrange
            var fileSet = new FileSet(null, new[] { new FileItem { FilePath = "Controlller.cs" }, new FileItem { FilePath = "Proj.csproj" } });
            var fileSetFactory = Mock.Of<IFileSetFactory>(f => f.CreateAsync(It.IsAny<CancellationToken>()) == Task.FromResult<FileSet>(fileSet));

            var filter = new TestableMSBuildEvaluationFilter(fileSetFactory)
            {
                Timestamps =
                {
                    ["Controller.cs"] = new DateTime(1000),
                    ["Proj.csproj"] = new DateTime(1000),
                }
            };

            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new()
            };

            var state = new WatchState()
            {
                Iteration = 0,
                ProcessSpec = new ProcessSpec()
            };

            await filter.ProcessAsync(context, state, CancellationToken.None);
            state.RequiresMSBuildRevaluation = false;
            state.ChangedFile = new FileItem { FilePath = "Controller.cs" };
            state.Iteration++;
            filter.Timestamps["Proj.csproj"] = new DateTime(1007);

            // Act
            await filter.ProcessAsync(context, state, CancellationToken.None);

            // Assert
            Assert.True(state.RequiresMSBuildRevaluation);
        }

        private class TestableMSBuildEvaluationFilter : MSBuildEvaluationFilter
        {
            public TestableMSBuildEvaluationFilter(IFileSetFactory factory)
                : base(factory)
            {
            }

            public Dictionary<string, DateTime> Timestamps { get; } = new Dictionary<string, DateTime>();

            private protected override DateTime GetLastWriteTimeUtcSafely(string file) => Timestamps[file];
        }
    }
}
