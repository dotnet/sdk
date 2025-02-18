// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Watch.UnitTests
{
    public class NoRestoreTests
    {
        private static DotNetWatchContext CreateContext(string[] args = null, EnvironmentOptions environmentOptions = null)
            => new()
            {
                Reporter = NullReporter.Singleton,
                Options = new(),
                RootProjectOptions = TestOptions.GetProjectOptions(args),
                EnvironmentOptions = environmentOptions ?? TestOptions.GetEnvironmentOptions(),
            };

        [Fact]
        public void LeavesArgumentsUnchangedOnFirstRun()
        {
            var context = CreateContext();
            var evaluator = new BuildEvaluator(context, new MockFileSetFactory());

            AssertEx.SequenceEqual(["run"], evaluator.GetProcessArguments(iteration: 0));
        }

        [Fact]
        public void LeavesArgumentsUnchangedIfMsBuildRevaluationIsRequired()
        {
            var context = CreateContext();
            var evaluator = new BuildEvaluator(context, new MockFileSetFactory());

            AssertEx.SequenceEqual(["run"], evaluator.GetProcessArguments(iteration: 0));

            evaluator.RequiresRevaluation = true;

            AssertEx.SequenceEqual(["run"], evaluator.GetProcessArguments(iteration: 1));
        }

        [Fact]
        public void LeavesArgumentsUnchangedIfOptimizationIsSuppressed()
        {
            var context = CreateContext([], TestOptions.GetEnvironmentOptions() with { SuppressMSBuildIncrementalism = true });
            var evaluator = new BuildEvaluator(context, new MockFileSetFactory());

            AssertEx.SequenceEqual(["run"], evaluator.GetProcessArguments(iteration: 0));
            AssertEx.SequenceEqual(["run"], evaluator.GetProcessArguments(iteration: 1));
        }

        [Fact]
        public void LeavesArgumentsUnchangedIfNoRestoreAlreadyPresent()
        {
            var context = CreateContext(["--no-restore"], TestOptions.GetEnvironmentOptions() with { SuppressMSBuildIncrementalism = true });
            var evaluator = new BuildEvaluator(context, new MockFileSetFactory());

            AssertEx.SequenceEqual(["run", "--no-restore"], evaluator.GetProcessArguments(iteration: 0));
            AssertEx.SequenceEqual(["run", "--no-restore"], evaluator.GetProcessArguments(iteration: 1));
        }

        [Fact]
        public void LeavesArgumentsUnchangedIfNoRestoreAlreadyPresent_UnlessAfterDashDash1()
        {
            var context = CreateContext(["--", "--no-restore"]);
            var evaluator = new BuildEvaluator(context, new MockFileSetFactory());

            AssertEx.SequenceEqual(["run", "--", "--no-restore"], evaluator.GetProcessArguments(iteration: 0));
            AssertEx.SequenceEqual(["run", "--no-restore", "--", "--no-restore"], evaluator.GetProcessArguments(iteration: 1));
        }

        [Fact]
        public void LeavesArgumentsUnchangedIfNoRestoreAlreadyPresent_UnlessAfterDashDash2()
        {
            var context = CreateContext(["--", "--", "--no-restore"]);
            var evaluator = new BuildEvaluator(context, new MockFileSetFactory());

            AssertEx.SequenceEqual(["run", "--", "--", "--no-restore"], evaluator.GetProcessArguments(iteration: 0));
            AssertEx.SequenceEqual(["run", "--no-restore", "--", "--", "--no-restore"], evaluator.GetProcessArguments(iteration: 1));
        }

        [Fact]
        public void AddsNoRestoreSwitch()
        {
            var context = CreateContext();
            var evaluator = new BuildEvaluator(context, new MockFileSetFactory());

            AssertEx.SequenceEqual(["run"], evaluator.GetProcessArguments(iteration: 0));
            AssertEx.SequenceEqual(["run", "--no-restore"], evaluator.GetProcessArguments(iteration: 1));
        }

        [Fact]
        public void AddsNoRestoreSwitch_WithAdditionalArguments()
        {
            var context = CreateContext(["run", "-f", ToolsetInfo.CurrentTargetFramework]);
            var evaluator = new BuildEvaluator(context, new MockFileSetFactory());

            AssertEx.SequenceEqual(["run", "-f", ToolsetInfo.CurrentTargetFramework], evaluator.GetProcessArguments(iteration: 0));
            AssertEx.SequenceEqual(["run", "--no-restore", "-f", ToolsetInfo.CurrentTargetFramework], evaluator.GetProcessArguments(iteration: 1));
        }

        [Fact]
        public void AddsNoRestoreSwitch_ForTestCommand()
        {
            var context = CreateContext(["test", "--filter SomeFilter"]);
            var evaluator = new BuildEvaluator(context, new MockFileSetFactory());

            AssertEx.SequenceEqual(["test", "--filter SomeFilter"], evaluator.GetProcessArguments(iteration: 0));
            AssertEx.SequenceEqual(["test", "--no-restore", "--filter SomeFilter"], evaluator.GetProcessArguments(iteration: 1));
        }

        [Fact]
        public void DoesNotModifyArgumentsForUnknownCommands()
        {
            var context = CreateContext(["pack"]);
            var evaluator = new BuildEvaluator(context, new MockFileSetFactory());

            AssertEx.SequenceEqual(["pack"], evaluator.GetProcessArguments(iteration: 0));
            AssertEx.SequenceEqual(["pack"], evaluator.GetProcessArguments(iteration: 1));
        }
    }
}
