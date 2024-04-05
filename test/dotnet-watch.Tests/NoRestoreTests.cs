// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    public class NoRestoreTests
    {
        private readonly string[] _arguments = new[] { "run" };

        [Fact]
        public void ProcessAsync_LeavesArgumentsUnchangedOnFirstRun()
        {
            var context = new DotNetWatchContext
            {
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new(),
                Options = TestOptions.CommandLine,
                EnvironmentOptions = TestOptions.Environmental,
            };

            var evaluator = new BuildEvaluator(context, new MockFileSetFactory());

            var processSpec = new ProcessSpec
            {
                Arguments = _arguments,
            };

            evaluator.UpdateProcessArguments(processSpec, iteration: 0);

            Assert.Same(_arguments, processSpec.Arguments);
        }

        [Fact]
        public void ProcessAsync_LeavesArgumentsUnchangedIfMsBuildRevaluationIsRequired()
        {
            var context = new DotNetWatchContext
            {
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new(),
                Options = TestOptions.CommandLine,
                EnvironmentOptions = TestOptions.Environmental,
            };

            var evaluator = new BuildEvaluator(context, new MockFileSetFactory());

            var processSpec = new ProcessSpec
            {
                Arguments = _arguments,
            };

            evaluator.UpdateProcessArguments(processSpec, iteration: 0);

            evaluator.RequiresRevaluation = true;

            evaluator.UpdateProcessArguments(processSpec, iteration: 1);

            Assert.Same(_arguments, processSpec.Arguments);
        }

        [Fact]
        public void ProcessAsync_LeavesArgumentsUnchangedIfOptimizationIsSuppressed()
        {
            var context = new DotNetWatchContext
            {
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new(),
                Options = TestOptions.CommandLine,
                EnvironmentOptions = TestOptions.Environmental with { SuppressMSBuildIncrementalism = true },
            };

            var evaluator = new BuildEvaluator(context, new MockFileSetFactory());

            var processSpec = new ProcessSpec
            {
                Arguments = _arguments,
            };

            evaluator.UpdateProcessArguments(processSpec, iteration: 0);
            evaluator.UpdateProcessArguments(processSpec, iteration: 1);
            Assert.Same(_arguments, processSpec.Arguments);
        }

        [Fact]
        public void ProcessAsync_AddsNoRestoreSwitch()
        {
            var context = new DotNetWatchContext
            {
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new(),
                Options = TestOptions.CommandLine,
                EnvironmentOptions = TestOptions.Environmental,
            };

            var processSpec = new ProcessSpec
            {
                Arguments = _arguments,
            };

            var evaluator = new BuildEvaluator(context, new MockFileSetFactory());

            evaluator.UpdateProcessArguments(processSpec, iteration: 0);
            evaluator.UpdateProcessArguments(processSpec, iteration: 1);

            Assert.Equal(new[] { "run", "--no-restore" }, processSpec.Arguments);
        }

        [Fact]
        public void ProcessAsync_AddsNoRestoreSwitch_WithAdditionalArguments()
        {
            var context = new DotNetWatchContext
            {
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new(),
                Options = TestOptions.CommandLine,
                EnvironmentOptions = TestOptions.Environmental,
            };

            var evaluator = new BuildEvaluator(context, new MockFileSetFactory());

            var processSpec = new ProcessSpec
            {
                Arguments = ["run", "-f", ToolsetInfo.CurrentTargetFramework, "--", "foo=bar"],
            };

            evaluator.UpdateProcessArguments(processSpec, iteration: 0);
            evaluator.UpdateProcessArguments(processSpec, iteration: 1);

            Assert.Equal(["run", "--no-restore", "-f", ToolsetInfo.CurrentTargetFramework, "--", "foo=bar"], processSpec.Arguments);
        }

        [Fact]
        public void ProcessAsync_AddsNoRestoreSwitch_ForTestCommand()
        {
            var context = new DotNetWatchContext
            {
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new(),
                Options = TestOptions.CommandLine,
                EnvironmentOptions = TestOptions.Environmental,
            };

            var evaluator = new BuildEvaluator(context, new MockFileSetFactory());

            var processSpec = new ProcessSpec
            {
                Arguments = ["test", "--filter SomeFilter"],
            };

            evaluator.UpdateProcessArguments(processSpec, iteration: 0);
            evaluator.UpdateProcessArguments(processSpec, iteration: 1);

            Assert.Equal(["test", "--no-restore", "--filter SomeFilter"], processSpec.Arguments);
        }

        [Fact]
        public void ProcessAsync_DoesNotModifyArgumentsForUnknownCommands()
        {
            var arguments = new[] { "ef", "database", "update" };

            var context = new DotNetWatchContext
            {
                Reporter = NullReporter.Singleton,
                LaunchSettingsProfile = new(),
                Options = TestOptions.CommandLine,
                EnvironmentOptions = TestOptions.Environmental,
            };

            var evaluator = new BuildEvaluator(context, new MockFileSetFactory());

            var processSpec = new ProcessSpec
            {
                Arguments = arguments,
            };

            evaluator.UpdateProcessArguments(processSpec, iteration: 0);
            evaluator.UpdateProcessArguments(processSpec, iteration: 1);

            Assert.Same(arguments, processSpec.Arguments);
        }
    }
}
