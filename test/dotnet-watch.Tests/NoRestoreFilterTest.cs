// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    public class NoRestoreFilterTest
    {
        private readonly string[] _arguments = new[] { "run" };

        [Fact]
        public void ProcessAsync_LeavesArgumentsUnchangedOnFirstRun()
        {
            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new(),
                Options = TestOptions.CommandLine,
                EnvironmentOptions = TestOptions.Environmental,
            };

            var filter = new NoRestoreFilter(context);

            var state = new WatchState()
            {
                ProcessSpec = new ProcessSpec
                {
                    Arguments = _arguments,
                }
            };

            filter.Process(state);

            Assert.Same(_arguments, state.ProcessSpec.Arguments);
        }

        [Fact]
        public void ProcessAsync_LeavesArgumentsUnchangedIfMsBuildRevaluationIsRequired()
        {
            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new(),
                Options = TestOptions.CommandLine,
                EnvironmentOptions = TestOptions.Environmental,
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

            filter.Process(state);

            state.ChangedFile = new FileItem { FilePath = "Test.proj" };
            state.RequiresMSBuildRevaluation = true;
            state.Iteration++;

            filter.Process(state);

            Assert.Same(_arguments, state.ProcessSpec.Arguments);
        }

        [Fact]
        public void ProcessAsync_LeavesArgumentsUnchangedIfOptimizationIsSuppressed()
        {
            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new(),
                Options = TestOptions.CommandLine,
                EnvironmentOptions = TestOptions.Environmental with { SuppressMSBuildIncrementalism = true },
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

            filter.Process(state);

            state.ChangedFile = new FileItem { FilePath = "Program.cs" };
            state.Iteration++;

            filter.Process(state);

            Assert.Same(_arguments, state.ProcessSpec.Arguments);
        }

        [Fact]
        public void ProcessAsync_AddsNoRestoreSwitch()
        {
            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new(),
                Options = TestOptions.CommandLine,
                EnvironmentOptions = TestOptions.Environmental,
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

            filter.Process(state);

            state.ChangedFile = new FileItem { FilePath = "Program.cs" };
            state.Iteration++;

            filter.Process(state);

            Assert.Equal(new[] { "run", "--no-restore" }, state.ProcessSpec.Arguments);
        }

        [Fact]
        public void ProcessAsync_AddsNoRestoreSwitch_WithAdditionalArguments()
        {
            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new(),
                Options = TestOptions.CommandLine,
                EnvironmentOptions = TestOptions.Environmental,
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

            filter.Process(state);

            state.ChangedFile = new FileItem { FilePath = "Program.cs" };
            state.Iteration++;

            filter.Process(state);

            Assert.Equal(["run", "--no-restore", "-f", ToolsetInfo.CurrentTargetFramework, "--", "foo=bar"], state.ProcessSpec.Arguments);
        }

        [Fact]
        public void ProcessAsync_AddsNoRestoreSwitch_ForTestCommand()
        {
            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new(),
                Options = TestOptions.CommandLine,
                EnvironmentOptions = TestOptions.Environmental,
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

            filter.Process(state);

            state.ChangedFile = new FileItem { FilePath = "Program.cs" };
            state.Iteration++;

            filter.Process(state);

            Assert.Equal(["test", "--no-restore", "--filter SomeFilter"], state.ProcessSpec.Arguments);
        }

        [Fact]
        public void ProcessAsync_DoesNotModifyArgumentsForUnknownCommands()
        {
            var arguments = new[] { "ef", "database", "update" };

            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new(),
                Options = TestOptions.CommandLine,
                EnvironmentOptions = TestOptions.Environmental,
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

            filter.Process(state);

            state.ChangedFile = new FileItem { FilePath = "Program.cs" };
            state.Iteration++;

            filter.Process(state);

            Assert.Same(arguments, state.ProcessSpec.Arguments);
        }
    }
}
