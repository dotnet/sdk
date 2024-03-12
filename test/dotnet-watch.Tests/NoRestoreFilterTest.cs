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
            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new()
            };

            var filter = new NoRestoreFilter(context);

            var state = new WatchState()
            {
                ProcessSpec = new ProcessSpec
                {
                    Arguments = _arguments,
                }
            };

            await filter.ProcessAsync(state, default);

            Assert.Same(_arguments, state.ProcessSpec.Arguments);
        }

        [Fact]
        public async Task ProcessAsync_LeavesArgumentsUnchangedIfMsBuildRevaluationIsRequired()
        {
            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new()
            };

            var filter = new NoRestoreFilter(context);

            var state = new WatchState()
            {
                Iteration = 0,
                ProcessSpec = new ProcessSpec
                {
                    Arguments = _arguments,
                }
            };

            await filter.ProcessAsync(state, CancellationToken.None);

            state.ChangedFile = new FileItem { FilePath = "Test.proj" };
            state.RequiresMSBuildRevaluation = true;
            state.Iteration++;

            await filter.ProcessAsync(state, CancellationToken.None);

            Assert.Same(_arguments, state.ProcessSpec.Arguments);
        }

        [Fact]
        public async Task ProcessAsync_LeavesArgumentsUnchangedIfOptimizationIsSuppressed()
        {
            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                SuppressMSBuildIncrementalism = true,
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new()
            };

            var filter = new NoRestoreFilter(context);

            var state = new WatchState()
            {
                Iteration = 0,
                ProcessSpec = new ProcessSpec
                {
                    Arguments = _arguments,
                }
            };

            await filter.ProcessAsync(state, CancellationToken.None);

            state.ChangedFile = new FileItem { FilePath = "Program.cs" };
            state.Iteration++;

            await filter.ProcessAsync(state, CancellationToken.None);

            Assert.Same(_arguments, state.ProcessSpec.Arguments);
        }

        [Fact]
        public async Task ProcessAsync_AddsNoRestoreSwitch()
        {
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

            var filter = new NoRestoreFilter(context);

            await filter.ProcessAsync(state, CancellationToken.None);

            state.ChangedFile = new FileItem { FilePath = "Program.cs" };
            state.Iteration++;

            await filter.ProcessAsync(state, CancellationToken.None);

            Assert.Equal(new[] { "run", "--no-restore" }, state.ProcessSpec.Arguments);
        }

        [Fact]
        public async Task ProcessAsync_AddsNoRestoreSwitch_WithAdditionalArguments()
        {
            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new()
            };

            var filter = new NoRestoreFilter(context);

            var state = new WatchState()
            {
                Iteration = 0,
                ProcessSpec = new ProcessSpec
                {
                    Arguments = ["run", "-f", ToolsetInfo.CurrentTargetFramework, "--", "foo=bar"],
                }
            };

            await filter.ProcessAsync(state, CancellationToken.None);

            state.ChangedFile = new FileItem { FilePath = "Program.cs" };
            state.Iteration++;

            await filter.ProcessAsync(state, CancellationToken.None);

            Assert.Equal(["run", "--no-restore", "-f", ToolsetInfo.CurrentTargetFramework, "--", "foo=bar"], state.ProcessSpec.Arguments);
        }

        [Fact]
        public async Task ProcessAsync_AddsNoRestoreSwitch_ForTestCommand()
        {
            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new()
            };

            var filter = new NoRestoreFilter(context);

            var state = new WatchState()
            {
                Iteration = 0,
                ProcessSpec = new ProcessSpec
                {
                    Arguments = ["test", "--filter SomeFilter"],
                }
            };

            await filter.ProcessAsync(state, CancellationToken.None);

            state.ChangedFile = new FileItem { FilePath = "Program.cs" };
            state.Iteration++;

            await filter.ProcessAsync(state, CancellationToken.None);

            Assert.Equal(["test", "--no-restore", "--filter SomeFilter"], state.ProcessSpec.Arguments);
        }

        [Fact]
        public async Task ProcessAsync_DoesNotModifyArgumentsForUnknownCommands()
        {
            var arguments = new[] { "ef", "database", "update" };

            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new()
            };

            var filter = new NoRestoreFilter(context);

            var state = new WatchState()
            {
                Iteration = 0,
                ProcessSpec = new ProcessSpec
                {
                    Arguments = arguments,
                }
            };

            await filter.ProcessAsync(state, CancellationToken.None);

            state.ChangedFile = new FileItem { FilePath = "Program.cs" };
            state.Iteration++;

            await filter.ProcessAsync(state, CancellationToken.None);

            Assert.Same(arguments, state.ProcessSpec.Arguments);
        }
    }
}
