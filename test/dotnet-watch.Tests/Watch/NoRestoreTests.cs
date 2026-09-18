// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class NoRestoreTests
{
    private static DotNetWatchContext CreateContext(string[] args = null, EnvironmentOptions environmentOptions = null)
    {
        environmentOptions ??= TestOptions.GetEnvironmentOptions();

        var processOutputReporter = new TestProcessOutputReporter();
        var cmdOptions = TestOptions.GetCommandLineOptions(args ?? []);
        var projectOptions = TestOptions.GetProjectOptions(cmdOptions);

        return new()
        {
            ProcessOutputReporter = processOutputReporter,
            LoggerFactory = NullLoggerFactory.Instance,
            Logger = NullLogger.Instance,
            BuildLogger = NullLogger.Instance,
            ProcessRunner = new ProcessRunner(processCleanupTimeout: TimeSpan.Zero),
            Options = new(),
            MainProjectOptions = projectOptions,
            RootProjects = [projectOptions.Representation],
            BuildArguments = cmdOptions.BuildArguments,
            EnvironmentOptions = environmentOptions,
            BrowserLauncher = new BrowserLauncher(NullLogger.Instance, processOutputReporter, environmentOptions),
            BrowserRefreshServerFactory = new BrowserRefreshServerFactory()
        };
    }

    [TestMethod]
    public void LeavesArgumentsUnchangedOnFirstRun()
    {
        var context = CreateContext();
        var evaluator = new BuildEvaluator(context);

        AssertProcessArguments(["run"], evaluator.GetProcessArguments(iteration: 0));
    }

    [TestMethod]
    public void AddsNoBuildWhenProjectWasBuiltBeforeLaunch()
    {
        var context = CreateContext();
        var evaluator = new BuildEvaluator(context);

        AssertProcessArguments(["run", "--no-build"], evaluator.GetProcessArguments(iteration: 0, skipBuild: true));
    }

    [TestMethod]
    public void LeavesArgumentsUnchangedIfMsBuildRevaluationIsRequired()
    {
        var context = CreateContext();
        var evaluator = new BuildEvaluator(context);

        AssertProcessArguments(["run"], evaluator.GetProcessArguments(iteration: 0));

        evaluator.RequiresRevaluation = true;

        AssertProcessArguments(["run"], evaluator.GetProcessArguments(iteration: 1));
    }

    [TestMethod]
    public void LeavesArgumentsUnchangedIfOptimizationIsSuppressed()
    {
        var context = CreateContext([], TestOptions.GetEnvironmentOptions() with { SuppressMSBuildIncrementalism = true });
        var evaluator = new BuildEvaluator(context);

        AssertProcessArguments(["run"], evaluator.GetProcessArguments(iteration: 0));
        AssertProcessArguments(["run"], evaluator.GetProcessArguments(iteration: 1));
    }

    [TestMethod]
    public void LeavesArgumentsUnchangedIfNoRestoreAlreadyPresent()
    {
        var context = CreateContext(["--no-restore"], TestOptions.GetEnvironmentOptions() with { SuppressMSBuildIncrementalism = true });
        var evaluator = new BuildEvaluator(context);

        AssertProcessArguments(["run", "--no-restore"], evaluator.GetProcessArguments(iteration: 0));
        AssertProcessArguments(["run", "--no-restore"], evaluator.GetProcessArguments(iteration: 1));
    }

    [TestMethod]
    public void LeavesArgumentsUnchangedIfNoRestoreAlreadyPresent_UnlessAfterDashDash1()
    {
        var context = CreateContext(["--", "--no-restore"]);
        var evaluator = new BuildEvaluator(context);

        AssertProcessArguments(["run", "--", "--no-restore"], evaluator.GetProcessArguments(iteration: 0));
        AssertProcessArguments(["run", "--no-restore", "--", "--no-restore"], evaluator.GetProcessArguments(iteration: 1));
    }

    [TestMethod]
    public void LeavesArgumentsUnchangedIfNoRestoreAlreadyPresent_UnlessAfterDashDash2()
    {
        var context = CreateContext(["--", "--", "--no-restore"]);
        var evaluator = new BuildEvaluator(context);

        AssertProcessArguments(["run", "--", "--", "--no-restore"], evaluator.GetProcessArguments(iteration: 0));
        AssertProcessArguments(["run", "--no-restore", "--", "--", "--no-restore"], evaluator.GetProcessArguments(iteration: 1));
    }

    [TestMethod]
    public void AddsNoRestoreSwitch()
    {
        var context = CreateContext();
        var evaluator = new BuildEvaluator(context);

        AssertProcessArguments(["run"], evaluator.GetProcessArguments(iteration: 0));
        AssertProcessArguments(["run", "--no-restore"], evaluator.GetProcessArguments(iteration: 1));
    }

    [TestMethod]
    public void AddsNoRestoreSwitch_WithAdditionalArguments()
    {
        var context = CreateContext(["run", "-f", ToolsetInfo.CurrentTargetFramework]);
        var evaluator = new BuildEvaluator(context);

        AssertProcessArguments(["run", "--framework", ToolsetInfo.CurrentTargetFramework], evaluator.GetProcessArguments(iteration: 0));
        AssertProcessArguments(["run", "--no-restore", "--framework", ToolsetInfo.CurrentTargetFramework], evaluator.GetProcessArguments(iteration: 1));
    }

    [TestMethod]
    public void AddsNoRestoreSwitch_ForTestCommand()
    {
        var context = CreateContext(["test", "--filter SomeFilter"]);
        var evaluator = new BuildEvaluator(context);

        AssertProcessArguments(["test", "--filter SomeFilter"], evaluator.GetProcessArguments(iteration: 0));
        AssertProcessArguments(["test", "--no-restore", "--filter SomeFilter"], evaluator.GetProcessArguments(iteration: 1));
    }

    [TestMethod]
    public void PreservesArgumentsForPackCommand()
    {
        var context = CreateContext(["pack"]);
        var evaluator = new BuildEvaluator(context);

        AssertProcessArguments(["pack"], evaluator.GetProcessArguments(iteration: 0));
        AssertProcessArguments(["pack"], evaluator.GetProcessArguments(iteration: 1));
    }

    [TestMethod]
    public void DoesNotAddReservedPropertiesToFormatCommand()
    {
        var context = CreateContext(["format", "--verbosity", "detailed"]);
        var evaluator = new BuildEvaluator(context);

        AssertEx.SequenceEqual(
            ["format", "--verbosity", "detailed"],
            evaluator.GetProcessArguments(iteration: 0));
    }

    [TestMethod]
    public void PassesApplicationArgumentsThrough()
    {
        var context = CreateContext(["--", "application-argument"]);
        var evaluator = new BuildEvaluator(context);

        AssertProcessArguments(
            ["run", "--", "application-argument"],
            evaluator.GetProcessArguments(iteration: 0));
    }

    private static void AssertProcessArguments(
        IEnumerable<string> expectedArguments,
        IReadOnlyList<string> actualArguments)
    {
        AssertEx.SequenceEqual(expectedArguments.ToList(), actualArguments);
    }
}
