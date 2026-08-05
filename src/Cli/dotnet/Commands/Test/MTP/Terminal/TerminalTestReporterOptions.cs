// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Test.Terminal;

internal sealed class TerminalTestReporterOptions
{
    /// <summary>
    /// Gets path to which all other paths in output should be relative.
    /// </summary>
    public string? BaseDirectory { get; init; }

    /// <summary>
    /// Gets a value indicating whether we should show passed tests.
    /// </summary>
    public bool ShowPassedTests { get; init; }

    /// <summary>
    /// Gets a value indicating whether we should show information about which assembly is the source of the data on screen. Turn this off when running directly from an exe to reduce noise, because the path will always be the same.
    /// </summary>
    public bool ShowAssembly { get; init; }

    /// <summary>
    /// Gets a value indicating whether we should show information about which assembly started or completed. Turn this off when running directly from an exe to reduce noise, because the path will always be the same.
    /// </summary>
    public bool ShowAssemblyStartAndComplete { get; init; }

    /// <summary>
    /// Gets minimum amount of tests to run.
    /// </summary>
    public int MinimumExpectedTests { get; init; }

    /// <summary>
    /// Gets a value indicating whether a run with no selected tests is successful.
    /// </summary>
    public bool AllowZeroTests { get; init; }

    /// <summary>
    /// Gets a value indicating whether we should write the progress periodically to screen. When ANSI is allowed we update the progress as often as we can.
    /// When ANSI is not allowed we never have progress.
    /// </summary>
    public bool ShowProgress { get; init; }

    /// <summary>
    /// Gets a value indicating whether the active tests should be visible when the progress is shown.
    /// </summary>
    public bool ShowActiveTests { get; init; }

    /// <summary>
    /// Gets a value indicating the ANSI mode.
    /// </summary>
    public AnsiMode AnsiMode { get; init; }

    /// <summary>
    /// Gets the format used when listing discovered tests ('--list-tests'). Only relevant in discovery mode.
    /// </summary>
    public TestListFormat ListTestsFormat { get; init; }

    /// <summary>
    /// Gets the number of slowest tests to list in the run summary. When greater than zero, a "Slowest tests"
    /// section ranking the longest-running tests (by their reported execution duration) is appended to the summary.
    /// Zero (the default) disables the section.
    /// </summary>
    public int SlowestTestsCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether tests that failed at least once but eventually passed after a retry are
    /// reported (the "flaky: N" summary line and the "Flaky tests" section). On by default; turned off by
    /// <c>--show-flaky-tests off</c>. Has no effect on a run where nothing was retried.
    /// </summary>
    public bool ShowFlakyTests { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the run-summary verdict and its counts are rendered. Upstream
    /// (Microsoft.Testing.Platform) turns this off for the second and later attempts of its in-process
    /// <c>--retry-failed-tests</c> orchestrator, whose summary would otherwise report the filtered subset that
    /// attempt re-ran as if it were the whole run. The 'dotnet test' orchestrator keeps a single reporter for the
    /// whole execution and aggregates every attempt into one tally, so it never turns this off; the property is
    /// kept so the hard fork stays shape-compatible with upstream. Everything else — produced artifacts, the
    /// slowest-tests section and the error recaps — is rendered regardless.
    /// </summary>
    public bool ShowRunSummary { get; init; } = true;
}

internal enum AnsiMode
{
    /// <summary>
    /// Disable ANSI escape codes.
    /// </summary>
    NoAnsi,

    /// <summary>
    /// Use simplified ANSI renderer, which colors output, but does not move cursor.
    /// This is used in compatible CI environments.
    /// </summary>
    SimpleAnsi,

    /// <summary>
    /// Enable ANSI escape codes, including cursor movement, when the capabilities of the console allow it.
    /// </summary>
    AnsiIfPossible,
}
