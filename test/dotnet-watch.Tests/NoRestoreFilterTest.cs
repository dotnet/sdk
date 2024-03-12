// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    public class NoRestoreFilterTest
    {
        private readonly string[] _arguments = new[] { "run" };

        [Fact]
        public async Task ProcessAsync_LeavesArgumentsUnchangedOnFirstRun()
        {
            var filter = new NoRestoreFilter();

            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new()
            };

            var state = new WatchState()
            {
                ProcessSpec = new ProcessSpec
                {
                    Arguments = _arguments,
                }
            };

            await filter.ProcessAsync(context, state, default);

            Assert.Same(_arguments, state.ProcessSpec.Arguments);
        }

        [Fact]
        public async Task ProcessAsync_LeavesArgumentsUnchangedIfMsBuildRevaluationIsRequired()
        {
            var filter = new NoRestoreFilter();

            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new()
            };

            var state = new WatchState()
            {
                Iteration = 0,
                ProcessSpec = new ProcessSpec
                {
                    Arguments = _arguments,
                }
            };

            await filter.ProcessAsync(context, state, CancellationToken.None);

            state.ChangedFile = new FileItem { FilePath = "Test.proj" };
            state.RequiresMSBuildRevaluation = true;
            state.Iteration++;

            await filter.ProcessAsync(context, state, CancellationToken.None);

            Assert.Same(_arguments, state.ProcessSpec.Arguments);
        }

        [Fact]
        public async Task ProcessAsync_LeavesArgumentsUnchangedIfOptimizationIsSuppressed()
        {
            var filter = new NoRestoreFilter();

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
                ProcessSpec = new ProcessSpec
                {
                    Arguments = _arguments,
                }
            };

            await filter.ProcessAsync(context, state, CancellationToken.None);

            state.ChangedFile = new FileItem { FilePath = "Program.cs" };
            state.Iteration++;

            await filter.ProcessAsync(context, state, CancellationToken.None);

            Assert.Same(_arguments, state.ProcessSpec.Arguments);
        }

        [Fact]
        public async Task ProcessAsync_AddsNoRestoreSwitch()
        {
            // Arrange
            var filter = new NoRestoreFilter();

            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new()
            };

            var state = new WatchState()
            {
                Iteration = 0,
                ProcessSpec = new ProcessSpec
                {
                    Arguments = _arguments,
                }
            };

            await filter.ProcessAsync(context, state, CancellationToken.None);

            state.ChangedFile = new FileItem { FilePath = "Program.cs" };
            state.Iteration++;

            // Act
            await filter.ProcessAsync(context, state, CancellationToken.None);

            // Assert
            Assert.Equal(new[] { "run", "--no-restore" }, state.ProcessSpec.Arguments);
        }

        [Fact]
        public async Task ProcessAsync_AddsNoRestoreSwitch_WithAdditionalArguments()
        {
            // Arrange
            var filter = new NoRestoreFilter();

            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new()
            };

            var state = new WatchState()
            {
                Iteration = 0,
                ProcessSpec = new ProcessSpec
                {
                    Arguments = ["run", "-f", ToolsetInfo.CurrentTargetFramework, "--", "foo=bar"],
                }
            };

            await filter.ProcessAsync(context, state, CancellationToken.None);

            state.ChangedFile = new FileItem { FilePath = "Program.cs" };
            state.Iteration++;

            // Act
            await filter.ProcessAsync(context, state, CancellationToken.None);

            // Assert
            Assert.Equal(["run", "--no-restore", "-f", ToolsetInfo.CurrentTargetFramework, "--", "foo=bar"], state.ProcessSpec.Arguments);
        }

        [Fact]
        public async Task ProcessAsync_AddsNoRestoreSwitch_ForTestCommand()
        {
            // Arrange
            var filter = new NoRestoreFilter();

            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new()
            };

            var state = new WatchState()
            {
                Iteration = 0,
                ProcessSpec = new ProcessSpec
                {
                    Arguments = ["test", "--filter SomeFilter"],
                }
            };

            await filter.ProcessAsync(context, state, CancellationToken.None);

            state.ChangedFile = new FileItem { FilePath = "Program.cs" };
            state.Iteration++;

            // Act
            await filter.ProcessAsync(context, state, CancellationToken.None);

            // Assert
            Assert.Equal(["test", "--no-restore", "--filter SomeFilter"], state.ProcessSpec.Arguments);
        }

        [Fact]
        public async Task ProcessAsync_DoesNotModifyArgumentsForUnknownCommands()
        {
            // Arrange
            var filter = new NoRestoreFilter();
            var arguments = new[] { "ef", "database", "update" };

            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new()
            };

            var state = new WatchState()
            {
                Iteration = 0,
                ProcessSpec = new ProcessSpec
                {
                    Arguments = arguments,
                }
            };

            await filter.ProcessAsync(context, state, CancellationToken.None);

            state.ChangedFile = new FileItem { FilePath = "Program.cs" };
            state.Iteration++;

            // Act
            await filter.ProcessAsync(context, state, CancellationToken.None);

            // Assert
            Assert.Same(arguments, state.ProcessSpec.Arguments);
        }
    }
}
