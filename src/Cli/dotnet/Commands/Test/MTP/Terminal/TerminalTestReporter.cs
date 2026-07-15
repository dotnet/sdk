// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Help;
using Microsoft.Testing.Platform.OutputDevice.Terminal;

namespace Microsoft.DotNet.Cli.Commands.Test.Terminal;

/// <summary>
/// Terminal test reporter that outputs test progress and is capable of writing ANSI or non-ANSI output via the given terminal.
/// </summary>
internal sealed partial class TerminalTestReporter : IDisposable
{
    /// <summary>
    /// The two directory separator characters to be passed to methods like <see cref="string.IndexOfAny(char[])"/>.
    /// </summary>
    private static readonly string[] NewLineStrings = ["\r\n", "\n"];

    internal const string SingleIndentation = "  ";

    internal const string DoubleIndentation = $"{SingleIndentation}{SingleIndentation}";

    internal Func<IStopwatch> CreateStopwatch { get; set; } = SystemStopwatch.StartNew;

    private readonly ConcurrentDictionary<string, TestProgressState> _assemblies = new();

    private readonly List<TestRunArtifact> _artifacts = [];

    private readonly TerminalTestReporterOptions _options;

    private readonly TestProgressStateAwareTerminal _terminalWithProgress;

    /// <summary>
    /// Whether to track and render currently running tests. Gated on both the caller-requested
    /// <see cref="TerminalTestReporterOptions.ShowActiveTests"/> and the effective progress
    /// capability of the console: if progress cannot actually be rendered (e.g. redirected stdout,
    /// non-TTY, or ANSI not supported) there is no point allocating per-test running-state.
    /// </summary>
    private readonly bool _showActiveTests;

    private int _handshakeFailuresCount;

    private readonly object _handshakeFailuresLock = new();
    private readonly List<HandshakeFailureRecord> _handshakeFailures = new();

    private readonly uint? _originalConsoleMode;
    private bool _isDiscovery;
    private bool _isHelp;
    private bool _isRetry;
    private DateTimeOffset? _testExecutionStartTime;

    private DateTimeOffset? _testExecutionEndTime;

    private int _buildErrorsCount;

    private bool _wasCancelled;

    public bool HasHandshakeFailure => _handshakeFailuresCount > 0;
    public int TotalTests => _assemblies.Values.Sum(a => a.TotalTests);

    // Specifying no timeout, the regex is linear. And the timeout does not measure the regex only, but measures also any
    // thread suspends, so the regex gets blamed incorrectly.
    [GeneratedRegex(@$"^   at ((?<code>.+) in (?<file>.+):line (?<line>\d+)|(?<code1>.+))$", RegexOptions.ExplicitCapture)]
    private static partial Regex GetFrameRegex();

    private int _counter;

    /// <summary>
    /// Initializes a new instance of the <see cref="TerminalTestReporter"/> class with custom terminal and manual refresh for testing.
    /// </summary>
    public TerminalTestReporter(IConsole console, TerminalTestReporterOptions options)
    {
        _options = options;
        bool showProgress = _options.ShowProgress;

        ITerminal terminal;
        if (_options.AnsiMode == AnsiMode.SimpleAnsi)
        {
            // We are told externally that we are in CI, use simplified ANSI mode.
            terminal = new SimpleAnsiTerminal(console);
            showProgress = false;
        }
        else
        {
            // We are not in CI, or in CI non-compatible with simple ANSI, autodetect terminal capabilities
            // Autodetect.
            (bool consoleAcceptsAnsiCodes, bool _, uint? originalConsoleMode) = NativeMethods.QueryIsScreenAndTryEnableAnsiColorCodes();
            _originalConsoleMode = originalConsoleMode;

            bool useAnsi = _options.AnsiMode switch
            {
                AnsiMode.NoAnsi => false,
                AnsiMode.AnsiIfPossible => consoleAcceptsAnsiCodes,
                _ => throw new UnreachableException(),
            };

            showProgress = showProgress && useAnsi;
            terminal = useAnsi ? new AnsiTerminal(console, _options.BaseDirectory) : new NonAnsiTerminal(console);
        }

        _terminalWithProgress = new TestProgressStateAwareTerminal(terminal, showProgress);
        _showActiveTests = _options.ShowActiveTests && showProgress;
    }

    public void TestExecutionStarted(DateTimeOffset testStartTime, int workerCount, bool isDiscovery, bool isHelp, bool isRetry)
    {
        _isDiscovery = isDiscovery;
        _isHelp = isHelp;
        _isRetry = isRetry;
        _testExecutionStartTime = testStartTime;
        _terminalWithProgress.StartShowingProgress(workerCount);
    }

    public void AssemblyRunStarted(string assembly, string? targetFramework, string? architecture, string executionId, string instanceId)
    {
        var assemblyRun = GetOrAddAssemblyRun(assembly, targetFramework, architecture, executionId);
        assemblyRun.NotifyHandshake(instanceId);

        // If we fail to parse out the parameter correctly this will enable retry on re-run of the assembly within the same execution.
        // Not good enough for general use, because we want to show (try 1) even on the first try, but this will at
        // least show (try 2) etc. So user is still aware there is retry going on, and counts of tests won't break.
        _isRetry |= assemblyRun.TryCount > 1;

        if (_options.ShowAssembly && _options.ShowAssemblyStartAndComplete)
        {
            _terminalWithProgress.WriteToTerminal(terminal =>
            {
                if (_isRetry)
                {
                    terminal.SetColor(TerminalColor.DarkGray);
                    terminal.Append($"({string.Format(CliCommandStrings.Try, assemblyRun.TryCount)}) ");
                    terminal.ResetColor();
                }

                terminal.Append(_isDiscovery ? CliCommandStrings.DiscoveringTestsFrom : CliCommandStrings.RunningTestsFrom);
                terminal.Append(' ');
                AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, assembly, targetFramework, architecture);
                terminal.AppendLine();
            });
        }
    }

    private TestProgressState GetOrAddAssemblyRun(string assembly, string? targetFramework, string? architecture, string executionId)
    {
        return _assemblies.GetOrAdd(executionId, _ =>
        {
            IStopwatch sw = CreateStopwatch();
            var assemblyRun = new TestProgressState(Interlocked.Increment(ref _counter), assembly, targetFramework, architecture, sw, _isDiscovery);
            int slotIndex = _terminalWithProgress.AddWorker(assemblyRun);
            assemblyRun.SlotIndex = slotIndex;

            return assemblyRun;
        });
    }

    public void TestExecutionCompleted(DateTimeOffset endTime, int? exitCode)
    {
        _testExecutionEndTime = endTime;
        _terminalWithProgress.StopShowingProgress();

        if (!_isHelp)
        {
            if (_isDiscovery)
            {
                _terminalWithProgress.WriteToTerminal(terminal => AppendTestDiscoverySummary(terminal, exitCode));
            }
            else
            {
                _terminalWithProgress.WriteToTerminal(terminal => AppendTestRunSummary(terminal, exitCode));
            }
        }

        NativeMethods.RestoreConsoleMode(_originalConsoleMode);
        _assemblies.Clear();
        lock (_handshakeFailuresLock)
        {
            _handshakeFailures.Clear();
        }
        _handshakeFailuresCount = 0;
        _buildErrorsCount = 0;
        _testExecutionStartTime = null;
        _testExecutionEndTime = null;
    }

    private void AppendTestRunSummary(ITerminal terminal, int? exitCode)
    {
        IEnumerable<IGrouping<bool, TestRunArtifact>> artifactGroups = _artifacts.GroupBy(a => a.OutOfProcess);

        if (artifactGroups.Any())
        {
            // Add extra empty line when we will be writing any artifacts, to split it from previous output.
            terminal.AppendLine();
        }

        foreach (IGrouping<bool, TestRunArtifact> artifactGroup in artifactGroups)
        {
            terminal.Append(SingleIndentation);
            terminal.AppendLine(artifactGroup.Key ? CliCommandStrings.OutOfProcessArtifactsProduced : CliCommandStrings.InProcessArtifactsProduced);
            foreach (TestRunArtifact artifact in artifactGroup)
            {
                terminal.Append(DoubleIndentation);
                terminal.Append("- ");
                if (!string.IsNullOrWhiteSpace(artifact.TestName))
                {
                    terminal.Append(CliCommandStrings.ForTest);
                    terminal.Append(" '");
                    terminal.Append(artifact.TestName);
                    terminal.Append("': ");
                }

                terminal.AppendLink(artifact.Path, lineNumber: null);
                terminal.AppendLine();
            }
        }

        terminal.AppendLine();

        int totalTests = _assemblies.Values.Sum(a => a.TotalTests);
        int totalFailedTests = _assemblies.Values.Sum(a => a.FailedTests);
        int totalSkippedTests = _assemblies.Values.Sum(a => a.SkippedTests);

        bool notEnoughTests = totalTests < _options.MinimumExpectedTests;
        bool allTestsWereSkipped = totalTests == 0 || totalTests == totalSkippedTests;
        bool anyTestFailed = totalFailedTests > 0;
        bool anyAssemblyFailed = _assemblies.Values.Any(a => !a.Success) || HasHandshakeFailure;
        bool runFailed = anyAssemblyFailed || anyTestFailed || notEnoughTests || allTestsWereSkipped || _wasCancelled;
        terminal.SetColor(runFailed ? TerminalColor.DarkRed : TerminalColor.DarkGreen);

        terminal.Append(CliCommandStrings.TestRunSummary);
        terminal.Append(' ');

        if (_wasCancelled)
        {
            terminal.Append(CliCommandStrings.Aborted);
        }
        else if (notEnoughTests)
        {
            terminal.Append(string.Format(CultureInfo.CurrentCulture, CliCommandStrings.MinimumExpectedTestsPolicyViolation, totalTests, _options.MinimumExpectedTests));
        }
        else if (anyTestFailed || HasHandshakeFailure)
        {
            // Handshake failures take precedence over "Zero tests ran": when an assembly failed to
            // hand-shake we want the headline to reflect that the run failed, not that no tests ran
            // (which would imply a benign empty run). We intentionally do NOT escalate the broader
            // anyAssemblyFailed here, because a project that legitimately contains zero tests exits
            // with ExitCodes.ZeroTests (non-zero) and would otherwise be misclassified as a failure.
            terminal.Append(string.Format(CultureInfo.CurrentCulture, "{0}!", CliCommandStrings.Failed));
        }
        else if (allTestsWereSkipped)
        {
            terminal.Append(CliCommandStrings.ZeroTestsRan);
        }
        else if (anyAssemblyFailed)
        {
            terminal.Append(string.Format(CultureInfo.CurrentCulture, "{0}!", CliCommandStrings.Failed));
        }
        else
        {
            terminal.Append(string.Format(CultureInfo.CurrentCulture, "{0}!", CliCommandStrings.Passed));
        }

        if (!_options.ShowAssembly && _assemblies.Count == 1)
        {
            TestProgressState testProgressState = _assemblies.Values.Single();
            terminal.SetColor(TerminalColor.DarkGray);
            terminal.Append(" - ");
            terminal.ResetColor();
            AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, testProgressState.Assembly, testProgressState.TargetFramework, testProgressState.Architecture);
        }

        terminal.AppendLine();

        if (_options.ShowAssembly && _assemblies.Count > 1)
        {
            foreach (TestProgressState assemblyRun in _assemblies.Values)
            {
                terminal.Append(SingleIndentation);
                AppendAssemblySummary(assemblyRun, terminal);
            }

            terminal.AppendLine();
        }

        int total = _assemblies.Values.Sum(t => t.TotalTests);
        int failed = _assemblies.Values.Sum(t => t.FailedTests);
        int passed = _assemblies.Values.Sum(t => t.PassedTests);
        int skipped = _assemblies.Values.Sum(t => t.SkippedTests);
        int retried = _assemblies.Values.Sum(t => t.RetriedFailedTests);

        // If the process exited with non-zero exit code (t.Success is false)
        // And also we didn't receive any failed tests, we consider these as errors.
        // In addition, failing to handshake is also considered as an error.
        // Note: In case of handshake failure, we shouldn't add any entries to _assemblies dictionary.
        // So, this line cannot be double-counting handshake failures twice.
        int error = _assemblies.Values.Count(t => !t.Success && t.FailedTests == 0) + _handshakeFailuresCount;
        TimeSpan runDuration = _testExecutionStartTime != null && _testExecutionEndTime != null ? (_testExecutionEndTime - _testExecutionStartTime).Value : TimeSpan.Zero;

        bool colorizeFailed = failed > 0;
        bool colorizeError = error > 0;
        bool colorizePassed = passed > 0 && _buildErrorsCount == 0 && failed == 0 && error == 0;
        bool colorizeSkipped = skipped > 0 && skipped == total && _buildErrorsCount == 0 && failed == 0 && error == 0;

        string errorText = $"{SingleIndentation}{CliCommandStrings.ErrorColon} {error}";
        string totalText = $"{SingleIndentation}{CliCommandStrings.TotalColon} {total}";
        string retriedText = $" (+{retried} {CliCommandStrings.Retried})";
        string failedText = $"{SingleIndentation}{CliCommandStrings.FailedColon} {failed}";
        string passedText = $"{SingleIndentation}{CliCommandStrings.SucceededColon} {passed}";
        string skippedText = $"{SingleIndentation}{CliCommandStrings.SkippedColon} {skipped}";
        string durationText = $"{SingleIndentation}{CliCommandStrings.DurationColon} ";

        if (error > 0)
        {
            terminal.SetColor(TerminalColor.DarkRed);
            terminal.AppendLine(errorText);
            terminal.ResetColor();
            terminal.AppendLine();
        }

        terminal.ResetColor();
        terminal.Append(totalText);
        if (retried > 0)
        {
            terminal.SetColor(TerminalColor.DarkGray);
            terminal.Append(retriedText);
            terminal.ResetColor();
        }
        terminal.AppendLine();

        if (colorizeFailed)
        {
            terminal.SetColor(TerminalColor.DarkRed);
        }

        terminal.AppendLine(failedText);

        if (colorizeFailed)
        {
            terminal.ResetColor();
        }

        if (colorizePassed)
        {
            terminal.SetColor(TerminalColor.DarkGreen);
        }

        terminal.AppendLine(passedText);

        if (colorizePassed)
        {
            terminal.ResetColor();
        }

        if (colorizeSkipped)
        {
            terminal.SetColor(TerminalColor.DarkYellow);
        }

        terminal.AppendLine(skippedText);

        if (colorizeSkipped)
        {
            terminal.ResetColor();
        }

        terminal.Append(durationText);
        AppendLongDuration(terminal, runDuration, wrapInParentheses: false, colorize: false);
        terminal.AppendLine();

        AppendExitCodeAndUrl(terminal, exitCode, isRun: true);

        AppendHandshakeFailureRecap(terminal);
    }

    private void AppendHandshakeFailureRecap(ITerminal terminal)
    {
        // Re-print handshake failures captured during the run so that — even when there is a lot of
        // diagnostic output before the summary — the user sees the actionable failure context
        // (assembly, exit code, stdout, stderr) at the end of the run rather than having to scroll
        // back. See https://github.com/dotnet/sdk/issues/51608.
        HandshakeFailureRecord[] failures;
        lock (_handshakeFailuresLock)
        {
            if (_handshakeFailures.Count == 0)
            {
                return;
            }

            failures = _handshakeFailures.ToArray();
        }

        terminal.AppendLine();
        terminal.SetColor(TerminalColor.DarkRed);
        terminal.AppendLine(CliCommandStrings.HandshakeFailuresHeader);
        terminal.ResetColor();

        foreach (HandshakeFailureRecord failure in failures)
        {
            terminal.Append(SingleIndentation);
            AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, failure.AssemblyPath, failure.TargetFramework, architecture: null);
            terminal.AppendLine();
            AppendExecutableSummary(terminal, failure.ExitCode, failure.OutputData, failure.ErrorData);
        }
    }

    private static void AppendExitCodeAndUrl(ITerminal terminal, int? exitCode, bool isRun)
    {
        // When we crash with exception we don't have any predetermined exit code, and won't write our helper message to point users to exit code overview.
        // When we succeed we also don't point users to exit code overview.
        if (exitCode == null || exitCode == 0)
        {
            return;
        }

        terminal.AppendLine(string.Format(isRun ? CliCommandStrings.TestRunExitCode : CliCommandStrings.TestDiscoveryExitCode, exitCode));
    }

    /// <summary>
    /// Print a build result summary to the output.
    /// </summary>
    private static void AppendAssemblyResult(ITerminal terminal, TestProgressState state)
    {
        if (!state.Success)
        {
            terminal.SetColor(TerminalColor.DarkRed);
            // If the build failed, we print one of three red strings.
            string text = (state.FailedTests > 0, state.TotalTests == 0) switch
            {
                (true, _) => string.Format(CultureInfo.CurrentCulture, CliCommandStrings.FailedWithErrors, state.FailedTests),
                (false, true) => CliCommandStrings.ZeroTestsRan,
                (false, false) => CliCommandStrings.FailedLowercase,
            };
            terminal.Append(text);
            terminal.ResetColor();
        }
        else
        {
            terminal.SetColor(TerminalColor.DarkGreen);
            terminal.Append(CliCommandStrings.PassedLowercase);
            terminal.ResetColor();
        }
    }

    internal void TestCompleted(
        string assembly,
        string? targetFramework,
        string? architecture,
        string executionId,
        string instanceId,
        string testNodeUid,
        string displayName,
        string? informativeMessage,
        TestOutcome outcome,
        TimeSpan? duration,
        FlatException[]? exceptions,
        string? expected,
        string? actual,
        string? standardOutput,
        string? errorOutput)
    {
        TestProgressState asm = _assemblies[executionId];
        var attempt = asm.TryCount;

        if (_showActiveTests)
        {
            asm.TestNodeResultsState?.RemoveRunningTestNode(instanceId, testNodeUid);
        }

        switch (outcome)
        {
            case TestOutcome.Error:
            case TestOutcome.Timeout:
            case TestOutcome.Canceled:
            case TestOutcome.Fail:
                asm.ReportFailedTest(testNodeUid, instanceId);
                break;
            case TestOutcome.Passed:
                asm.ReportPassingTest(testNodeUid, instanceId);
                break;
            case TestOutcome.Skipped:
                asm.ReportSkippedTest(testNodeUid, instanceId);
                break;
        }

        _terminalWithProgress.UpdateWorker(asm.SlotIndex);
        if (outcome != TestOutcome.Passed || _options.ShowPassedTests)
        {
            _terminalWithProgress.WriteToTerminal(terminal => RenderTestCompleted(
                terminal,
                assembly,
                attempt,
                targetFramework,
                architecture,
                displayName,
                informativeMessage,
                outcome,
                duration,
                exceptions,
                expected,
                actual,
                standardOutput,
                errorOutput));
        }
    }

    internal /* for testing */ void RenderTestCompleted(
        ITerminal terminal,
        string assembly,
        int attempt,
        string? targetFramework,
        string? architecture,
        string displayName,
        string? informativeMessage,
        TestOutcome outcome,
        TimeSpan? duration,
        FlatException[]? flatExceptions,
        string? expected,
        string? actual,
        string? standardOutput,
        string? errorOutput)
    {
        if (outcome == TestOutcome.Passed && !_options.ShowPassedTests)
        {
            return;
        }

        TerminalColor color = outcome switch
        {
            TestOutcome.Error or TestOutcome.Fail or TestOutcome.Canceled or TestOutcome.Timeout => TerminalColor.DarkRed,
            TestOutcome.Skipped => TerminalColor.DarkYellow,
            TestOutcome.Passed => TerminalColor.DarkGreen,
            _ => throw new NotSupportedException(),
        };
        string outcomeText = outcome switch
        {
            TestOutcome.Fail or TestOutcome.Error => CliCommandStrings.FailedLowercase,
            TestOutcome.Skipped => CliCommandStrings.SkippedLowercase,
            TestOutcome.Canceled or TestOutcome.Timeout => $"{CliCommandStrings.FailedLowercase} ({CliCommandStrings.CancelledLowercase})",
            TestOutcome.Passed => CliCommandStrings.PassedLowercase,
            _ => throw new NotSupportedException(),
        };

        terminal.SetColor(color);
        terminal.Append(outcomeText);
        if (_isRetry)
        {
            terminal.SetColor(TerminalColor.DarkGray);
            terminal.Append($" ({string.Format(CliCommandStrings.Try, attempt)})");
        }
        terminal.ResetColor();
        terminal.Append(' ');
        terminal.Append(displayName);

        if (duration.HasValue)
        {
            terminal.Append(' ');
            AppendLongDuration(terminal, duration.Value);
        }

        if (!string.IsNullOrEmpty(informativeMessage))
        {
            terminal.AppendLine();
            terminal.Append(SingleIndentation);
            terminal.Append(informativeMessage);
        }

        if (_options.ShowAssembly)
        {
            terminal.AppendLine();
            terminal.Append(SingleIndentation);
            terminal.Append(CliCommandStrings.FromFile);
            terminal.Append(' ');
            AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, assembly, targetFramework, architecture);
        }

        terminal.AppendLine();

        FormatErrorMessage(terminal, flatExceptions, outcome, 0);
        FormatExpectedAndActual(terminal, expected, actual);
        FormatStackTrace(terminal, flatExceptions, 0);
        FormatInnerExceptions(terminal, flatExceptions);
        FormatStandardAndErrorOutput(terminal, standardOutput, errorOutput);
    }

    private static void FormatInnerExceptions(ITerminal terminal, FlatException[]? exceptions)
    {
        if (exceptions is null || exceptions.Length == 0)
        {
            return;
        }

        for (int i = 1; i < exceptions.Length; i++)
        {
            terminal.SetColor(TerminalColor.DarkRed);
            terminal.Append(SingleIndentation);
            terminal.Append("--->");
            FormatErrorMessage(terminal, exceptions, TestOutcome.Error, i);
            FormatStackTrace(terminal, exceptions, i);
        }
    }

    private static void FormatErrorMessage(ITerminal terminal, FlatException[]? exceptions, TestOutcome outcome, int index)
    {
        string? firstErrorMessage = GetStringFromIndexOrDefault(exceptions, e => e.ErrorMessage, index);
        string? firstErrorType = GetStringFromIndexOrDefault(exceptions, e => e.ErrorType, index);
        string? firstStackTrace = GetStringFromIndexOrDefault(exceptions, e => e.StackTrace, index);
        if (string.IsNullOrWhiteSpace(firstErrorMessage) && string.IsNullOrWhiteSpace(firstErrorType) && string.IsNullOrWhiteSpace(firstStackTrace))
        {
            return;
        }

        terminal.SetColor(TerminalColor.DarkRed);

        if (firstStackTrace is null)
        {
            AppendIndentedLine(terminal, firstErrorMessage, SingleIndentation);
        }
        else if (outcome == TestOutcome.Fail)
        {
            // For failed tests, we don't prefix the message with the exception type because it is most likely an assertion specific exception like AssertionFailedException, and we prefer to show that without the exception type to avoid additional noise.
            AppendIndentedLine(terminal, firstErrorMessage, SingleIndentation);
        }
        else
        {
            AppendIndentedLine(terminal, $"{firstErrorType}: {firstErrorMessage}", SingleIndentation);
        }

        terminal.ResetColor();
    }

    private static string? GetStringFromIndexOrDefault(FlatException[]? exceptions, Func<FlatException, string?> property, int index) =>
        exceptions != null && exceptions.Length >= index + 1 ? property(exceptions[index]) : null;

    private static void FormatExpectedAndActual(ITerminal terminal, string? expected, string? actual)
    {
        if (string.IsNullOrWhiteSpace(expected) && string.IsNullOrWhiteSpace(actual))
        {
            return;
        }

        terminal.SetColor(TerminalColor.DarkRed);
        terminal.Append(SingleIndentation);
        terminal.AppendLine(CliCommandStrings.Expected);
        AppendIndentedLine(terminal, expected, DoubleIndentation);
        terminal.Append(SingleIndentation);
        terminal.AppendLine(CliCommandStrings.Actual);
        AppendIndentedLine(terminal, actual, DoubleIndentation);
        terminal.ResetColor();
    }

    private static void FormatStackTrace(ITerminal terminal, FlatException[]? exceptions, int index)
    {
        string? stackTrace = GetStringFromIndexOrDefault(exceptions, e => e.StackTrace, index);
        if (string.IsNullOrWhiteSpace(stackTrace))
        {
            return;
        }

        terminal.SetColor(TerminalColor.DarkGray);

        string[] lines = stackTrace.Split(NewLineStrings, StringSplitOptions.None);
        foreach (string line in lines)
        {
            AppendStackFrame(terminal, line);
        }

        terminal.ResetColor();
    }

    private static void FormatStandardAndErrorOutput(ITerminal terminal, string? standardOutput, string? standardError)
    {
        if (string.IsNullOrWhiteSpace(standardOutput) && string.IsNullOrWhiteSpace(standardError))
        {
            return;
        }

        terminal.SetColor(TerminalColor.DarkGray);
        terminal.Append(SingleIndentation);
        terminal.AppendLine(CliCommandStrings.StandardOutput);
        string? standardOutputWithoutSpecialChars = NormalizeSpecialCharacters(standardOutput);
        AppendIndentedLine(terminal, standardOutputWithoutSpecialChars, DoubleIndentation);
        terminal.Append(SingleIndentation);
        terminal.AppendLine(CliCommandStrings.StandardError);
        string? standardErrorWithoutSpecialChars = NormalizeSpecialCharacters(standardError);
        AppendIndentedLine(terminal, standardErrorWithoutSpecialChars, DoubleIndentation);
        terminal.ResetColor();
    }

    private static void AppendAssemblyLinkTargetFrameworkAndArchitecture(ITerminal terminal, string assembly, string? targetFramework, string? architecture)
    {
        terminal.AppendLink(assembly, lineNumber: null);
        if (targetFramework != null || architecture != null)
        {
            terminal.Append(" (");
            if (targetFramework != null)
            {
                terminal.Append(targetFramework);
                if (architecture != null)
                {
                    terminal.Append('|');
                }
            }

            if (architecture != null)
            {
                terminal.Append(architecture);
            }

            terminal.Append(')');
        }
    }

    internal /* for testing */ static void AppendStackFrame(ITerminal terminal, string stackTraceLine)
    {
        terminal.Append(DoubleIndentation);
        Match match = GetFrameRegex().Match(stackTraceLine);
        if (match.Success)
        {
            bool weHaveFilePathAndCodeLine = !string.IsNullOrWhiteSpace(match.Groups["code"].Value);
            terminal.Append(CliCommandStrings.StackFrameAt);
            terminal.Append(' ');

            if (weHaveFilePathAndCodeLine)
            {
                terminal.Append(match.Groups["code"].Value);
            }
            else
            {
                terminal.Append(match.Groups["code1"].Value);
            }

            if (weHaveFilePathAndCodeLine)
            {
                terminal.Append(' ');
                terminal.Append(CliCommandStrings.StackFrameIn);
                terminal.Append(' ');
                if (!string.IsNullOrWhiteSpace(match.Groups["file"].Value))
                {
                    int line = int.TryParse(match.Groups["line"].Value, out int value) ? value : 0;
                    terminal.AppendLink(match.Groups["file"].Value, line);

                    // AppendLink finishes by resetting color
                    terminal.SetColor(TerminalColor.DarkGray);
                }
            }

            terminal.AppendLine();
        }
        else
        {
            terminal.AppendLine(stackTraceLine);
        }
    }

    private static void AppendIndentedLine(ITerminal terminal, string? message, string indent)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (!message.Contains('\n'))
        {
            terminal.Append(indent);
            terminal.AppendLine(message);
            return;
        }

        string[] lines = message.Split(NewLineStrings, StringSplitOptions.None);
        foreach (string line in lines)
        {
            // Here we could check if the messages are longer than then line, and reflow them so a long line is split into multiple
            // and prepended by the respective indentation.
            // But this does not play nicely with ANSI escape codes. And if you
            // run in narrow terminal and then widen it the text does not reflow correctly. And you also have harder time copying
            // values when the assertion message is longer.
            terminal.Append(indent);
            terminal.Append(line);
            terminal.AppendLine();
        }
    }

    internal void AssemblyRunCompleted(string executionId,
        // These parameters are useful only for "remote" runs in dotnet test, where we are reporting on multiple processes.
        // In single process run, like with testing platform .exe we report these via messages, and run exit.
        int exitCode, string? outputData, string? errorData)
    {
        // Defense in depth: under the current TestApplicationHandler routing this branch is
        // unreachable in normal operation. OnTestProcessExited only calls AssemblyRunCompleted
        // when the TestHost handshake was received (which in turn caused AssemblyRunStarted to
        // register the executionId via GetOrAddAssemblyRun); controller-only handshakes go to
        // HandshakeFailure directly. The fallback below exists only to keep stray completions
        // (e.g. one that arrives after TestExecutionCompleted's _assemblies.Clear()) or a future
        // routing regression from surfacing as a KeyNotFoundException that would bury the real
        // exit cause.
        if (!_assemblies.TryGetValue(executionId, out TestProgressState? assemblyRun))
        {
            HandshakeFailure(assemblyPath: string.Empty, targetFramework: null, exitCode, outputData ?? string.Empty, errorData ?? string.Empty);
            return;
        }

        assemblyRun.Success = exitCode == 0 && assemblyRun.FailedTests == 0;
        assemblyRun.Stopwatch.Stop();

        _terminalWithProgress.RemoveWorker(assemblyRun.SlotIndex);

        if (!_isHelp && !_isDiscovery && _options.ShowAssembly && _options.ShowAssemblyStartAndComplete)
        {
            _terminalWithProgress.WriteToTerminal(terminal => AppendAssemblySummary(assemblyRun, terminal));
        }

        if (exitCode == 0)
        {
            // Report nothing, we don't want to report on success, because then we will also report on test-discovery etc.
            return;
        }

        _terminalWithProgress.WriteToTerminal(terminal =>
        {
            AppendExecutableSummary(terminal, exitCode, outputData, errorData);
        });
    }

    internal void HandshakeFailure(string assemblyPath, string? targetFramework, int exitCode, string outputData, string errorData, bool reportEvenWhenHelp = false)
    {
        if (_isHelp && !reportEvenWhenHelp)
        {
            // Backward-compat workaround: older Microsoft.Testing.Platform versions don't perform
            // a handshake on the --help path (the host just prints help and exits). In that case
            // OnTestProcessExited routes here with the "process exited without a usable handshake"
            // payload, which is expected and should not be reported as a failure.
            //
            // Newer MTP versions (microsoft/testfx#8794) always handshake — including on --help —
            // so this workaround is only needed while older MTP versions are still in use. Explicit
            // protocol-level rejections (e.g. ExecutionMode mismatch) pass reportEvenWhenHelp=true
            // and are still surfaced.
            return;
        }

        Interlocked.Increment(ref _handshakeFailuresCount);
        lock (_handshakeFailuresLock)
        {
            _handshakeFailures.Add(new HandshakeFailureRecord(assemblyPath, targetFramework, exitCode, outputData, errorData));
        }

        _terminalWithProgress.WriteToTerminal(terminal =>
        {
            terminal.ResetColor();
            AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, assemblyPath, targetFramework, architecture: null);
            terminal.Append(' ');
            terminal.SetColor(TerminalColor.DarkRed);
            terminal.Append(CliCommandStrings.ZeroTestsRan);
            terminal.ResetColor();
            terminal.AppendLine();
            AppendExecutableSummary(terminal, exitCode, outputData, errorData);
        });
    }

    private static void AppendExecutableSummary(ITerminal terminal, int? exitCode, string? outputData, string? errorData)
    {
        terminal.Append(CliCommandStrings.ExitCode);
        terminal.Append(": ");
        terminal.AppendLine(exitCode?.ToString(CultureInfo.CurrentCulture) ?? "<null>");
        AppendOutputWhenPresent(CliCommandStrings.StandardOutput, outputData);
        AppendOutputWhenPresent(CliCommandStrings.StandardError, errorData);

        void AppendOutputWhenPresent(string description, string? output)
        {
            if (!string.IsNullOrWhiteSpace(output))
            {
                AppendIndentedLine(terminal, $"{description}: {output}", SingleIndentation);
            }
        }
    }

    private static string? NormalizeSpecialCharacters(string? text)
        => text?.Replace('\0', '\x2400')
            // escape char
            .Replace('\x001b', '\x241b');

    private static void AppendAssemblySummary(TestProgressState assemblyRun, ITerminal terminal)
    {
        terminal.ResetColor();

        AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, assemblyRun.Assembly, assemblyRun.TargetFramework, assemblyRun.Architecture);
        terminal.Append(' ');
        AppendAssemblyResult(terminal, assemblyRun);
        terminal.Append(' ');
        AppendAssemblyTestCounts(terminal, assemblyRun);
        terminal.Append(' ');
        AppendLongDuration(terminal, assemblyRun.Stopwatch.Elapsed);
        terminal.AppendLine();
    }

    /// <summary>
    /// Renders a compact, per-assembly counts block that mirrors the in-progress indicator.
    /// Full-ANSI terminals get "[✓P/xF/↓S]" (with optional "/rR"); simple terminals
    /// (NoAnsi / SimpleAnsi / CI) get the ASCII "[+P/xF/?S]" form so logs stay font- and
    /// encoding-friendly. Glyphs and colors are intentionally kept in sync with the rendering in
    /// <see cref="AnsiTerminalTestProgressFrame"/> and <see cref="SimpleTerminal.RenderProgress"/>.
    /// </summary>
    private static void AppendAssemblyTestCounts(ITerminal terminal, TestProgressState assemblyRun)
    {
        int failed = assemblyRun.FailedTests;
        int passed = assemblyRun.PassedTests;
        int skipped = assemblyRun.SkippedTests;
        int retried = assemblyRun.RetriedFailedTests;

        bool unicode = terminal is AnsiTerminal;
        char passedGlyph = unicode ? '✓' : '+';
        char skippedGlyph = unicode ? '↓' : '?';

        terminal.Append('[');

        AppendGlyphCount(terminal, passedGlyph, passed, TerminalColor.DarkGreen);
        terminal.Append('/');
        AppendGlyphCount(terminal, 'x', failed, TerminalColor.DarkRed);
        terminal.Append('/');
        AppendGlyphCount(terminal, skippedGlyph, skipped, TerminalColor.DarkYellow);

        if (retried > 0)
        {
            terminal.Append('/');
            AppendGlyphCount(terminal, 'r', retried, TerminalColor.Gray);
        }

        terminal.Append(']');
    }

    private static void AppendGlyphCount(ITerminal terminal, char glyph, int count, TerminalColor color)
    {
        terminal.SetColor(color);
        terminal.Append(glyph);
        terminal.Append(count.ToString(CultureInfo.CurrentCulture));
        terminal.ResetColor();
    }

    /// <summary>
    /// Appends a long duration in human readable format such as 1h 23m 500ms.
    /// </summary>
    private static void AppendLongDuration(ITerminal terminal, TimeSpan duration, bool wrapInParentheses = true, bool colorize = true)
    {
        if (colorize)
        {
            terminal.SetColor(TerminalColor.DarkGray);
        }

        HumanReadableDurationFormatter.Append(terminal, duration, wrapInParentheses);

        if (colorize)
        {
            terminal.ResetColor();
        }
    }

    public void Dispose() => _terminalWithProgress.Dispose();

    public void ArtifactAdded(bool outOfProcess, string? assembly, string? targetFramework, string? architecture, string? executionId, string? testName, string path)
        => _artifacts.Add(new TestRunArtifact(outOfProcess, assembly, targetFramework, architecture, executionId, testName, path));

    internal void WriteMessage(string text) =>
        _terminalWithProgress.WriteToTerminal(terminal =>
        {
            terminal.Append(text);
        });

    internal void WriteWarningMessage(string text) =>
        _terminalWithProgress.WriteToTerminal(terminal =>
        {
            terminal.SetColor(TerminalColor.DarkYellow);
            terminal.AppendLine(text);
            terminal.ResetColor();
        });

    internal void WriteErrorMessage(string text) =>
        _terminalWithProgress.WriteToTerminal(terminal =>
        {
            terminal.SetColor(TerminalColor.DarkRed);
            terminal.AppendLine(text);
            terminal.ResetColor();
        });

    /// <summary>
    /// Let the user know that cancellation was triggered.
    /// </summary>
    public void StartCancelling()
    {
        if (_wasCancelled)
        {
            return;
        }

        _wasCancelled = true;
        _terminalWithProgress.WriteToTerminal(terminal =>
        {
            terminal.AppendLine();
            terminal.AppendLine(CliCommandStrings.CancellingTestSession);
            terminal.AppendLine(CliCommandStrings.PressCtrlCAgainToForceExit);
            terminal.AppendLine();
        });
    }

    internal void TestDiscovered(
        string executionId,
        string? displayName,
        string? uid,
        string? filePath,
        int? lineNumber)
    {
        if (!_isDiscovery)
        {
            // Don't count discovered tests if we are not in discovery mode.
            // NOTE: DiscoverTest call increments PassedTests, so it must not be called when we get discovery message in run mode.
            // Also, don't bother adding discovered tests to the DiscoveredTests list when the list is only used in discovery mode.
            return;
        }

        TestProgressState asm = _assemblies[executionId];

        // TODO: add mode for discovered tests to the progress bar - jajares
        asm.DiscoverTest(displayName, uid, filePath, lineNumber);
        _terminalWithProgress.UpdateWorker(asm.SlotIndex);
    }

    public void AppendTestDiscoverySummary(ITerminal terminal, int? exitCode)
    {
        terminal.AppendLine();

        var assemblies = _assemblies.Select(asm => asm.Value).OrderBy(a => a.Assembly).Where(a => a is not null).ToList();

        int totalTests = _assemblies.Values.Sum(a => a.TotalTests);
        bool runFailed = _wasCancelled || totalTests < 1;

        foreach (TestProgressState assembly in assemblies)
        {
            terminal.Append(string.Format(CultureInfo.CurrentCulture, CliCommandStrings.DiscoveredTestsInAssembly, assembly.DiscoveredTestNames.Count));
            terminal.Append(" - ");
            AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, assembly.Assembly, assembly.TargetFramework, assembly.Architecture);
            terminal.AppendLine();
            foreach ((string? displayName, string? uid, string? filePath, int? lineNumber) in assembly.DiscoveredTestNames)
            {
                if (displayName is not null)
                {
                    terminal.Append(SingleIndentation);
                    terminal.Append(displayName);
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        terminal.Append(" [");
                        terminal.Append(filePath);
                        if (lineNumber is > 0)
                        {
                            terminal.Append(':');
                            terminal.Append(lineNumber.Value.ToString(CultureInfo.InvariantCulture));
                        }

                        terminal.Append(']');
                    }

                    terminal.AppendLine();
                }
            }

            terminal.AppendLine();
        }

        terminal.SetColor(runFailed ? TerminalColor.DarkRed : TerminalColor.DarkGreen);
        if (assemblies.Count <= 1)
        {
            terminal.AppendLine(string.Format(CultureInfo.CurrentCulture, CliCommandStrings.TestDiscoverySummarySingular, totalTests));
        }
        else
        {
            terminal.AppendLine(string.Format(CultureInfo.CurrentCulture, CliCommandStrings.TestDiscoverySummary, totalTests, assemblies.Count));
        }

        terminal.ResetColor();
        terminal.AppendLine();

        if (_wasCancelled)
        {
            terminal.Append(CliCommandStrings.Aborted);
            terminal.AppendLine();
        }

        AppendExitCodeAndUrl(terminal, exitCode, isRun: false);
    }

    public void AssemblyDiscoveryCompleted(int testCount) =>
        _terminalWithProgress.WriteToTerminal(terminal => terminal.Append($"Found {testCount} tests"));

    private static TerminalColor ToTerminalColor(ConsoleColor consoleColor)
        => consoleColor switch
        {
            ConsoleColor.Black => TerminalColor.Black,
            ConsoleColor.DarkBlue => TerminalColor.DarkBlue,
            ConsoleColor.DarkGreen => TerminalColor.DarkGreen,
            ConsoleColor.DarkCyan => TerminalColor.DarkCyan,
            ConsoleColor.DarkRed => TerminalColor.DarkRed,
            ConsoleColor.DarkMagenta => TerminalColor.DarkMagenta,
            ConsoleColor.DarkYellow => TerminalColor.DarkYellow,
            ConsoleColor.DarkGray => TerminalColor.DarkGray,
            ConsoleColor.Gray => TerminalColor.Gray,
            ConsoleColor.Blue => TerminalColor.Blue,
            ConsoleColor.Green => TerminalColor.Green,
            ConsoleColor.Cyan => TerminalColor.Cyan,
            ConsoleColor.Red => TerminalColor.Red,
            ConsoleColor.Magenta => TerminalColor.Magenta,
            ConsoleColor.Yellow => TerminalColor.Yellow,
            ConsoleColor.White => TerminalColor.White,
            _ => TerminalColor.Default,
        };

    public void TestInProgress(
        string assembly,
        string? targetFramework,
        string? architecture,
        string executionId,
        string instanceId,
        string testNodeUid,
        string displayName)
    {
        TestProgressState asm = _assemblies[executionId];

        if (_showActiveTests)
        {
            asm.TestNodeResultsState ??= new(Interlocked.Increment(ref _counter));
            asm.TestNodeResultsState.AddRunningTestNode(
                Interlocked.Increment(ref _counter), instanceId, testNodeUid, displayName, CreateStopwatch());
        }

        _terminalWithProgress.UpdateWorker(asm.SlotIndex);
    }

    private readonly record struct HandshakeFailureRecord(
        string AssemblyPath,
        string? TargetFramework,
        int ExitCode,
        string OutputData,
        string ErrorData);
}
