// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Testing.Platform.Helpers;
using LocalizableStrings = Microsoft.DotNet.Tools.Test.LocalizableStrings;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

/// <summary>
/// Terminal test reporter that outputs test progress and is capable of writing ANSI or non-ANSI output via the given terminal.
/// </summary>
internal sealed partial class TerminalTestReporter : IDisposable
{
    /// <summary>
    /// The two directory separator characters to be passed to methods like <see cref="string.IndexOfAny(char[])"/>.
    /// </summary>
    private static readonly string[] NewLineStrings = { "\r\n", "\n" };

    internal const string SingleIndentation = "  ";

    internal const string DoubleIndentation = $"{SingleIndentation}{SingleIndentation}";

    internal Func<IStopwatch> CreateStopwatch { get; set; } = SystemStopwatch.StartNew;

    internal event EventHandler OnProgressStartUpdate
    {
        add => _terminalWithProgress.OnProgressStartUpdate += value;
        remove => _terminalWithProgress.OnProgressStartUpdate -= value;
    }

    internal event EventHandler OnProgressStopUpdate
    {
        add => _terminalWithProgress.OnProgressStopUpdate += value;
        remove => _terminalWithProgress.OnProgressStopUpdate -= value;
    }

    private readonly ConcurrentDictionary<string, TestProgressState> _assemblies = new();

    private readonly List<TestRunArtifact> _artifacts = new();

    private readonly TerminalTestReporterOptions _options;

    private readonly TestProgressStateAwareTerminal _terminalWithProgress;

    private readonly uint? _originalConsoleMode;
    private bool _isDiscovery;
    private bool _isHelp;
    private DateTimeOffset? _testExecutionStartTime;

    private DateTimeOffset? _testExecutionEndTime;

    private int _buildErrorsCount;

    private bool _wasCancelled;

    private bool? _shouldShowPassedTests;

#if NET7_0_OR_GREATER
    // Specifying no timeout, the regex is linear. And the timeout does not measure the regex only, but measures also any
    // thread suspends, so the regex gets blamed incorrectly.
    [GeneratedRegex(@$"^   at ((?<code>.+) in (?<file>.+):line (?<line>\d+)|(?<code1>.+))$", RegexOptions.ExplicitCapture)]
    private static partial Regex GetFrameRegex();
#else
    private static Regex? s_regex;

    [MemberNotNull(nameof(s_regex))]
    private static Regex GetFrameRegex()
    {
        if (s_regex != null)
        {
            return s_regex;
        }

        string atResourceName = "Word_At";
        string inResourceName = "StackTrace_InFileLineNumber";

        string? atString = null;
        string? inString = null;

        // Grab words from localized resource, in case the stack trace is localized.
        try
        {
            // Get these resources: https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/Resources/Strings.resx
#pragma warning disable RS0030 // Do not use banned APIs
            MethodInfo? getResourceStringMethod = typeof(Environment).GetMethod(
                "GetResourceString",
                BindingFlags.Static | BindingFlags.NonPublic, null, [typeof(string)], null);
#pragma warning restore RS0030 // Do not use banned APIs
            if (getResourceStringMethod is not null)
            {
                // <value>at</value>
                atString = (string?)getResourceStringMethod.Invoke(null, [atResourceName]);

                // <value>in {0}:line {1}</value>
                inString = (string?)getResourceStringMethod.Invoke(null, [inResourceName]);
            }
        }
        catch
        {
            // If we fail, populate the defaults below.
        }

        atString = atString == null || atString == atResourceName ? "at" : atString;
        inString = inString == null || inString == inResourceName ? "in {0}:line {1}" : inString;

        string inPattern = string.Format(CultureInfo.InvariantCulture, inString, "(?<file>.+)", @"(?<line>\d+)");

        // Specifying no timeout, the regex is linear. And the timeout does not measure the regex only, but measures also any
        // thread suspends, so the regex gets blamed incorrectly.
        s_regex = new Regex(@$"^   {atString} ((?<code>.+) {inPattern}|(?<code1>.+))$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        return s_regex;
    }
#endif

    private int _counter;

    /// <summary>
    /// Initializes a new instance of the <see cref="TerminalTestReporter"/> class with custom terminal and manual refresh for testing.
    /// </summary>
    public TerminalTestReporter(IConsole console, TerminalTestReporterOptions options)
    {
        _options = options;

        Func<bool?> showProgress = _options.ShowProgress;
        TestProgressStateAwareTerminal terminalWithProgress;

        // When not writing to ANSI we write the progress to screen and leave it there so we don't want to write it more often than every few seconds.
        int nonAnsiUpdateCadenceInMs = 3_000;
        // When writing to ANSI we update the progress in place and it should look responsive so we update every half second, because we only show seconds on the screen, so it is good enough.
        int ansiUpdateCadenceInMs = 500;
        if (!_options.UseAnsi || _options.ForceAnsi is false)
        {
            terminalWithProgress = new TestProgressStateAwareTerminal(new NonAnsiTerminal(console), showProgress, writeProgressImmediatelyAfterOutput: false, updateEvery: nonAnsiUpdateCadenceInMs);
        }
        else
        {
            // Autodetect.
            (bool consoleAcceptsAnsiCodes, bool _, uint? originalConsoleMode) = NativeMethods.QueryIsScreenAndTryEnableAnsiColorCodes();
            _originalConsoleMode = originalConsoleMode;
            terminalWithProgress = consoleAcceptsAnsiCodes || _options.ForceAnsi is true
                ? new TestProgressStateAwareTerminal(new AnsiTerminal(console, _options.BaseDirectory), showProgress, writeProgressImmediatelyAfterOutput: true, updateEvery: ansiUpdateCadenceInMs)
                : new TestProgressStateAwareTerminal(new NonAnsiTerminal(console), showProgress, writeProgressImmediatelyAfterOutput: false, updateEvery: nonAnsiUpdateCadenceInMs);
        }

        _terminalWithProgress = terminalWithProgress;
    }

    public void TestExecutionStarted(DateTimeOffset testStartTime, int workerCount, bool isDiscovery, bool isHelp)
    {
        _isDiscovery = isDiscovery;
        _isHelp = isHelp;
        _testExecutionStartTime = testStartTime;
        _terminalWithProgress.StartShowingProgress(workerCount);
    }

    public void AssemblyRunStarted(string assembly, string? targetFramework, string? architecture, string? executionId)
    {
        if (_options.ShowAssembly && _options.ShowAssemblyStartAndComplete)
        {
            _terminalWithProgress.WriteToTerminal(terminal =>
            {
                terminal.Append(_isDiscovery ? LocalizableStrings.DiscoveringTestsFrom : LocalizableStrings.RunningTestsFrom);
                terminal.Append(' ');
                AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, assembly, targetFramework, architecture);
                terminal.AppendLine();
            });
        }

        GetOrAddAssemblyRun(assembly, targetFramework, architecture, executionId);
    }

    private TestProgressState GetOrAddAssemblyRun(string assembly, string? targetFramework, string? architecture, string? executionId)
    {
        string key = $"{assembly}|{targetFramework}|{architecture}|{executionId}";
        return _assemblies.GetOrAdd(key, _ =>
        {
            IStopwatch sw = CreateStopwatch();
            var assemblyRun = new TestProgressState(Interlocked.Increment(ref _counter), assembly, targetFramework, architecture, sw);
            int slotIndex = _terminalWithProgress.AddWorker(assemblyRun);
            assemblyRun.SlotIndex = slotIndex;

            return assemblyRun;
        });
    }

    public void TestExecutionCompleted(DateTimeOffset endTime)
    {
        _testExecutionEndTime = endTime;
        _terminalWithProgress.StopShowingProgress();

        if (!_isHelp)
        {
            _terminalWithProgress.WriteToTerminal(_isDiscovery ? AppendTestDiscoverySummary : AppendTestRunSummary);
        }

        NativeMethods.RestoreConsoleMode(_originalConsoleMode);
        _assemblies.Clear();
        _buildErrorsCount = 0;
        _testExecutionStartTime = null;
        _testExecutionEndTime = null;
    }

    private void AppendTestRunSummary(ITerminal terminal)
    {
        terminal.AppendLine();

        IEnumerable<IGrouping<bool, TestRunArtifact>> artifactGroups = _artifacts.GroupBy(a => a.OutOfProcess);
        if (artifactGroups.Any())
        {
            terminal.AppendLine();
        }

        foreach (IGrouping<bool, TestRunArtifact> artifactGroup in artifactGroups)
        {
            terminal.Append(SingleIndentation);
            terminal.AppendLine(artifactGroup.Key ? LocalizableStrings.OutOfProcessArtifactsProduced : LocalizableStrings.InProcessArtifactsProduced);
            foreach (TestRunArtifact artifact in artifactGroup)
            {
                terminal.Append(DoubleIndentation);
                terminal.Append("- ");
                if (!String.IsNullOrWhiteSpace(artifact.TestName))
                {
                    terminal.Append(LocalizableStrings.ForTest);
                    terminal.Append(" '");
                    terminal.Append(artifact.TestName);
                    terminal.Append("': ");
                }

                terminal.AppendLink(artifact.Path, lineNumber: null);
                terminal.AppendLine();
            }
        }

        int totalTests = _assemblies.Values.Sum(a => a.TotalTests);
        int totalFailedTests = _assemblies.Values.Sum(a => a.FailedTests);
        int totalSkippedTests = _assemblies.Values.Sum(a => a.SkippedTests);

        bool notEnoughTests = totalTests < _options.MinimumExpectedTests;
        bool allTestsWereSkipped = totalTests == 0 || totalTests == totalSkippedTests;
        bool anyTestFailed = totalFailedTests > 0;
        bool anyAssemblyFailed = _assemblies.Values.Any(a => !a.Success);
        bool runFailed = anyAssemblyFailed || anyTestFailed || notEnoughTests || allTestsWereSkipped || _wasCancelled;
        terminal.SetColor(runFailed ? TerminalColor.Red : TerminalColor.Green);

        terminal.Append(LocalizableStrings.TestRunSummary);
        terminal.Append(' ');

        if (_wasCancelled)
        {
            terminal.Append(LocalizableStrings.Aborted);
        }
        else if (notEnoughTests)
        {
            terminal.Append(string.Format(CultureInfo.CurrentCulture, LocalizableStrings.MinimumExpectedTestsPolicyViolation, totalTests, _options.MinimumExpectedTests));
        }
        else if (allTestsWereSkipped)
        {
            terminal.Append(LocalizableStrings.ZeroTestsRan);
        }
        else if (anyTestFailed || anyAssemblyFailed)
        {
            terminal.Append(string.Format(CultureInfo.CurrentCulture, "{0}!", LocalizableStrings.Failed));
        }
        else
        {
            terminal.Append(string.Format(CultureInfo.CurrentCulture, "{0}!", LocalizableStrings.Passed));
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
                terminal.AppendLine();
            }

            terminal.AppendLine();
        }

        int total = _assemblies.Values.Sum(t => t.TotalTests);
        int failed = _assemblies.Values.Sum(t => t.FailedTests);
        int passed = _assemblies.Values.Sum(t => t.PassedTests);
        int skipped = _assemblies.Values.Sum(t => t.SkippedTests);
        int error = _assemblies.Values.Sum(t => !t.Success && (t.TotalTests == 0 || t.FailedTests == 0) ? 1 : 0);
        TimeSpan runDuration = _testExecutionStartTime != null && _testExecutionEndTime != null ? (_testExecutionEndTime - _testExecutionStartTime).Value : TimeSpan.Zero;

        bool colorizeFailed = failed > 0;
        bool colorizeError = error > 0;
        bool colorizePassed = passed > 0 && _buildErrorsCount == 0 && failed == 0 && error == 0;
        bool colorizeSkipped = skipped > 0 && skipped == total && _buildErrorsCount == 0 && failed == 0 && error == 0;

        string errorText = $"{SingleIndentation}error: {error}";
        string totalText = $"{SingleIndentation}total: {total}";
        string failedText = $"{SingleIndentation}failed: {failed}";
        string passedText = $"{SingleIndentation}succeeded: {passed}";
        string skippedText = $"{SingleIndentation}skipped: {skipped}";
        string durationText = $"{SingleIndentation}duration: ";

        if (error > 0)
        {
            terminal.SetColor(TerminalColor.Red);
            terminal.AppendLine(errorText);
            terminal.ResetColor();
            terminal.AppendLine();
        }

        terminal.ResetColor();
        terminal.AppendLine(totalText);
        if (colorizeFailed)
        {
            terminal.SetColor(TerminalColor.Red);
        }

        terminal.AppendLine(failedText);

        if (colorizeFailed)
        {
            terminal.ResetColor();
        }

        if (colorizePassed)
        {
            terminal.SetColor(TerminalColor.Green);
        }

        terminal.AppendLine(passedText);

        if (colorizePassed)
        {
            terminal.ResetColor();
        }

        if (colorizeSkipped)
        {
            terminal.SetColor(TerminalColor.Yellow);
        }

        terminal.AppendLine(skippedText);

        if (colorizeSkipped)
        {
            terminal.ResetColor();
        }

        terminal.Append(durationText);
        AppendLongDuration(terminal, runDuration, wrapInParentheses: false, colorize: false);
        terminal.AppendLine();
    }

    /// <summary>
    /// Print a build result summary to the output.
    /// </summary>
    private static void AppendAssemblyResult(ITerminal terminal, bool succeeded, int countErrors, int countWarnings)
    {
        if (!succeeded)
        {
            terminal.SetColor(TerminalColor.Red);
            // If the build failed, we print one of three red strings.
            string text = (countErrors > 0, countWarnings > 0) switch
            {
                (true, true) => string.Format(CultureInfo.CurrentCulture, LocalizableStrings.FailedWithErrorsAndWarnings, countErrors, countWarnings),
                (true, _) => string.Format(CultureInfo.CurrentCulture, LocalizableStrings.FailedWithErrors, countErrors),
                (false, true) => string.Format(CultureInfo.CurrentCulture, LocalizableStrings.FailedWithWarnings, countWarnings),
                _ => LocalizableStrings.FailedLowercase,
            };
            terminal.Append(text);
            terminal.ResetColor();
        }
        else if (countWarnings > 0)
        {
            terminal.SetColor(TerminalColor.Yellow);
            terminal.Append($"succeeded with {countWarnings} warning(s)");
            terminal.ResetColor();
        }
        else
        {
            terminal.SetColor(TerminalColor.Green);
            terminal.Append(LocalizableStrings.PassedLowercase);
            terminal.ResetColor();
        }
    }

    internal void TestCompleted(
       string assembly,
       string? targetFramework,
       string? architecture,
       string? executionId,
       string testNodeUid,
       string displayName,
       TestOutcome outcome,
       TimeSpan duration,
       string? errorMessage,
       Exception? exception,
       string? expected,
       string? actual,
       string? standardOutput,
       string? errorOutput)
    {
        FlatException[] flatExceptions = ExceptionFlattener.Flatten(errorMessage, exception);
        TestCompleted(
            assembly,
            targetFramework,
            architecture,
            executionId,
            testNodeUid,
            displayName,
            outcome,
            duration,
            flatExceptions,
            expected,
            actual,
            standardOutput,
            errorOutput);
    }

    internal void TestCompleted(
        string assembly,
        string? targetFramework,
        string? architecture,
        string? executionId,
        string testNodeUid,
        string displayName,
        TestOutcome outcome,
        TimeSpan duration,
        FlatException[] exceptions,
        string? expected,
        string? actual,
        string? standardOutput,
        string? errorOutput)
    {
        TestProgressState asm = _assemblies[$"{assembly}|{targetFramework}|{architecture}|{executionId}"];

        if (_options.ShowActiveTests)
        {
            asm.TestNodeResultsState?.RemoveRunningTestNode(testNodeUid);
        }

        switch (outcome)
        {
            case TestOutcome.Error:
            case TestOutcome.Timeout:
            case TestOutcome.Canceled:
            case TestOutcome.Fail:
                asm.FailedTests++;
                asm.TotalTests++;
                break;
            case TestOutcome.Passed:
                asm.PassedTests++;
                asm.TotalTests++;
                break;
            case TestOutcome.Skipped:
                asm.SkippedTests++;
                asm.TotalTests++;
                break;
        }

        _terminalWithProgress.UpdateWorker(asm.SlotIndex);
        if (outcome != TestOutcome.Passed || GetShowPassedTests())
        {
            _terminalWithProgress.WriteToTerminal(terminal => RenderTestCompleted(
                terminal,
                assembly,
                targetFramework,
                architecture,
                displayName,
                outcome,
                duration,
                exceptions,
                expected,
                actual,
                standardOutput,
                errorOutput));
        }
    }

    private bool GetShowPassedTests()
    {
        _shouldShowPassedTests ??= _options.ShowPassedTests();
        return _shouldShowPassedTests.Value;
    }

    internal /* for testing */ void RenderTestCompleted(
        ITerminal terminal,
        string assembly,
        string? targetFramework,
        string? architecture,
        string displayName,
        TestOutcome outcome,
        TimeSpan duration,
        FlatException[] flatExceptions,
        string? expected,
        string? actual,
        string? standardOutput,
        string? errorOutput)
    {
        if (outcome == TestOutcome.Passed && !GetShowPassedTests())
        {
            return;
        }

        TerminalColor color = outcome switch
        {
            TestOutcome.Error or TestOutcome.Fail or TestOutcome.Canceled or TestOutcome.Timeout => TerminalColor.Red,
            TestOutcome.Skipped => TerminalColor.Yellow,
            TestOutcome.Passed => TerminalColor.Green,
            _ => throw new NotSupportedException(),
        };
        string outcomeText = outcome switch
        {
            TestOutcome.Fail or TestOutcome.Error => LocalizableStrings.FailedLowercase,
            TestOutcome.Skipped => LocalizableStrings.SkippedLowercase,
            TestOutcome.Canceled or TestOutcome.Timeout => $"{LocalizableStrings.FailedLowercase} ({LocalizableStrings.CancelledLowercase})",
            TestOutcome.Passed => LocalizableStrings.PassedLowercase,
            _ => throw new NotSupportedException(),
        };

        terminal.SetColor(color);
        terminal.Append(outcomeText);
        terminal.ResetColor();
        terminal.Append(' ');
        terminal.Append(displayName);
        terminal.SetColor(TerminalColor.DarkGray);
        terminal.Append(' ');
        AppendLongDuration(terminal, duration);
        if (_options.ShowAssembly)
        {
            terminal.AppendLine();
            terminal.Append(SingleIndentation);
            terminal.Append(LocalizableStrings.FromFile);
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

    private static void FormatInnerExceptions(ITerminal terminal, FlatException[] exceptions)
    {
        if (exceptions is null || exceptions.Length == 0)
        {
            return;
        }

        for (int i = 1; i < exceptions.Length; i++)
        {
            terminal.SetColor(TerminalColor.Red);
            terminal.Append(SingleIndentation);
            terminal.Append("--->");
            FormatErrorMessage(terminal, exceptions, TestOutcome.Error, i);
            FormatStackTrace(terminal, exceptions, i);
        }
    }

    private static void FormatErrorMessage(ITerminal terminal, FlatException[] exceptions, TestOutcome outcome, int index)
    {
        string? firstErrorMessage = GetStringFromIndexOrDefault(exceptions, e => e.ErrorMessage, index);
        string? firstErrorType = GetStringFromIndexOrDefault(exceptions, e => e.ErrorType, index);
        string? firstStackTrace = GetStringFromIndexOrDefault(exceptions, e => e.StackTrace, index);
        if (String.IsNullOrWhiteSpace(firstErrorMessage) && String.IsNullOrWhiteSpace(firstErrorType) && String.IsNullOrWhiteSpace(firstStackTrace))
        {
            return;
        }

        terminal.SetColor(TerminalColor.Red);

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

    private static string? GetStringFromIndexOrDefault(FlatException[] exceptions, Func<FlatException, string?> property, int index) =>
        exceptions != null && exceptions.Length >= index + 1 ? property(exceptions[index]) : null;

    private static void FormatExpectedAndActual(ITerminal terminal, string? expected, string? actual)
    {
        if (String.IsNullOrWhiteSpace(expected) && String.IsNullOrWhiteSpace(actual))
        {
            return;
        }

        terminal.SetColor(TerminalColor.Red);
        terminal.Append(SingleIndentation);
        terminal.AppendLine(LocalizableStrings.Expected);
        AppendIndentedLine(terminal, expected, DoubleIndentation);
        terminal.Append(SingleIndentation);
        terminal.AppendLine(LocalizableStrings.Actual);
        AppendIndentedLine(terminal, actual, DoubleIndentation);
        terminal.ResetColor();
    }

    private static void FormatStackTrace(ITerminal terminal, FlatException[] exceptions, int index)
    {
        string? stackTrace = GetStringFromIndexOrDefault(exceptions, e => e.StackTrace, index);
        if (String.IsNullOrWhiteSpace(stackTrace))
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
        if (String.IsNullOrWhiteSpace(standardOutput) && String.IsNullOrWhiteSpace(standardError))
        {
            return;
        }

        terminal.SetColor(TerminalColor.DarkGray);
        terminal.Append(SingleIndentation);
        terminal.AppendLine(LocalizableStrings.StandardOutput);
        string? standardOutputWithoutSpecialChars = NormalizeSpecialCharacters(standardOutput);
        AppendIndentedLine(terminal, standardOutputWithoutSpecialChars, DoubleIndentation);
        terminal.Append(SingleIndentation);
        terminal.AppendLine(LocalizableStrings.StandardError);
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
                terminal.Append('|');
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
            bool weHaveFilePathAndCodeLine = !String.IsNullOrWhiteSpace(match.Groups["code"].Value);
            terminal.Append(LocalizableStrings.StackFrameAt);
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
                terminal.Append(LocalizableStrings.StackFrameIn);
                terminal.Append(' ');
                if (!String.IsNullOrWhiteSpace(match.Groups["file"].Value))
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
        if (String.IsNullOrWhiteSpace(message))
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

    internal void AssemblyRunCompleted(string assembly, string? targetFramework, string? architecture, string? executionId,
        // These parameters are useful only for "remote" runs in dotnet test, where we are reporting on multiple processes.
        // In single process run, like with testing platform .exe we report these via messages, and run exit.
        int? exitCode, string? outputData, string? errorData)
    {
        TestProgressState assemblyRun = GetOrAddAssemblyRun(assembly, targetFramework, architecture, executionId);
        assemblyRun.ExitCode = exitCode;
        assemblyRun.Success = exitCode == 0 && assemblyRun.FailedTests == 0;
        assemblyRun.Stopwatch.Stop();

        _terminalWithProgress.RemoveWorker(assemblyRun.SlotIndex);

        if (!_isHelp && !_isDiscovery && _options.ShowAssembly && _options.ShowAssemblyStartAndComplete)
        {
            _terminalWithProgress.WriteToTerminal(terminal => AppendAssemblySummary(assemblyRun, terminal));
        }

        if (exitCode is null or 0)
        {
            // Report nothing, we don't want to report on success, because then we will also report on test-discovery etc.
            return;
        }

        _terminalWithProgress.WriteToTerminal(terminal =>
        {
            AppendExecutableSummary(terminal, exitCode, outputData, errorData);
            terminal.AppendLine();
        });
    }

    private static void AppendExecutableSummary(ITerminal terminal, int? exitCode, string? outputData, string? errorData)
    {
        terminal.AppendLine();
        terminal.Append(LocalizableStrings.ExitCode);
        terminal.Append(": ");
        terminal.AppendLine(exitCode?.ToString(CultureInfo.CurrentCulture) ?? "<null>");
        terminal.Append(LocalizableStrings.StandardOutput);
        terminal.AppendLine(":");
        terminal.AppendLine(String.IsNullOrWhiteSpace(outputData) ? string.Empty : outputData);
        terminal.Append(LocalizableStrings.StandardError);
        terminal.AppendLine(":");
        terminal.AppendLine(String.IsNullOrWhiteSpace(errorData) ? string.Empty : errorData);
    }

    private static string? NormalizeSpecialCharacters(string? text)
        => text?.Replace('\0', '\x2400')
            // escape char
            .Replace('\x001b', '\x241b');

    private static void AppendAssemblySummary(TestProgressState assemblyRun, ITerminal terminal)
    {
        int failedTests = assemblyRun.FailedTests;
        int warnings = 0;

        AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, assemblyRun.Assembly, assemblyRun.TargetFramework, assemblyRun.Architecture);
        terminal.Append(' ');
        AppendAssemblyResult(terminal, assemblyRun.Success, failedTests, warnings);
        terminal.Append(' ');
        AppendLongDuration(terminal, assemblyRun.Stopwatch.Elapsed);
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

    /// <summary>
    /// Let the user know that cancellation was triggered.
    /// </summary>
    public void StartCancelling()
    {
        _wasCancelled = true;
        _terminalWithProgress.WriteToTerminal(terminal =>
        {
            terminal.AppendLine();
            terminal.AppendLine(LocalizableStrings.CancellingTestSession);
            terminal.AppendLine();
        });
    }

    internal void WriteErrorMessage(string assembly, string? targetFramework, string? architecture, string? executionId, string text, int? padding)
    {
        TestProgressState asm = GetOrAddAssemblyRun(assembly, targetFramework, architecture, executionId);
        asm.AddError(text);

        _terminalWithProgress.WriteToTerminal(terminal =>
        {
            terminal.SetColor(TerminalColor.Red);
            if (padding == null)
            {
                terminal.AppendLine(text);
            }
            else
            {
                AppendIndentedLine(terminal, text, new string(' ', padding.Value));
            }

            terminal.ResetColor();
        });
    }

    internal void WriteWarningMessage(string assembly, string? targetFramework, string? architecture, string? executionId, string text, int? padding)
    {
        TestProgressState asm = GetOrAddAssemblyRun(assembly, targetFramework, architecture, executionId);
        asm.AddWarning(text);
        _terminalWithProgress.WriteToTerminal(terminal =>
        {
            terminal.SetColor(TerminalColor.Yellow);
            if (padding == null)
            {
                terminal.AppendLine(text);
            }
            else
            {
                AppendIndentedLine(terminal, text, new string(' ', padding.Value));
            }

            terminal.ResetColor();
        });
    }

    internal void WriteErrorMessage(string assembly, string? targetFramework, string? architecture, string? executionId, Exception exception)
        => WriteErrorMessage(assembly, targetFramework, architecture, executionId, exception.ToString(), padding: null);

    public void WriteMessage(string text, SystemConsoleColor? color = null, int? padding = null)
    {
        if (color != null)
        {
            _terminalWithProgress.WriteToTerminal(terminal =>
            {
                terminal.SetColor(ToTerminalColor(color.ConsoleColor));
                if (padding == null)
                {
                    terminal.AppendLine(text);
                }
                else
                {
                    AppendIndentedLine(terminal, text, new string(' ', padding.Value));
                }

                terminal.ResetColor();
            });
        }
        else
        {
            _terminalWithProgress.WriteToTerminal(terminal =>
            {
                if (padding == null)
                {
                    terminal.AppendLine(text);
                }
                else
                {
                    AppendIndentedLine(terminal, text, new string(' ', padding.Value));
                }
            });
        }
    }

    internal void TestDiscovered(
        string assembly,
        string? targetFramework,
        string? architecture,
        string? executionId,
        string? displayName,
        string? uid)
    {
        TestProgressState asm = _assemblies[$"{assembly}|{targetFramework}|{architecture}|{executionId}"];

        // TODO: add mode for discovered tests to the progress bar - jajares
        asm.PassedTests++;
        asm.TotalTests++;
        asm.DiscoveredTests.Add(new(displayName, uid));
        _terminalWithProgress.UpdateWorker(asm.SlotIndex);
    }

    public void AppendTestDiscoverySummary(ITerminal terminal)
    {
        terminal.AppendLine();

        var assemblies = _assemblies.Select(asm => asm.Value).OrderBy(a => a.Assembly).Where(a => a is not null).ToList();

        int totalTests = _assemblies.Values.Sum(a => a.TotalTests);
        bool runFailed = _wasCancelled;

        foreach (TestProgressState assembly in assemblies)
        {
            terminal.Append(string.Format(CultureInfo.CurrentCulture, LocalizableStrings.DiscoveredTestsInAssembly, assembly.DiscoveredTests.Count));
            terminal.Append(" - ");
            AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, assembly.Assembly, assembly.TargetFramework, assembly.Architecture);
            terminal.AppendLine();
            foreach ((string? displayName, string? uid) in assembly.DiscoveredTests)
            {
                if (displayName is not null)
                {
                    terminal.Append(SingleIndentation);
                    terminal.AppendLine(displayName);
                }
            }

            terminal.AppendLine();
        }

        terminal.SetColor(runFailed ? TerminalColor.Red : TerminalColor.Green);
        if (assemblies.Count <= 1)
        {
            terminal.AppendLine(string.Format(CultureInfo.CurrentCulture, LocalizableStrings.TestDiscoverySummarySingular, totalTests));
        }
        else
        {
            terminal.AppendLine(string.Format(CultureInfo.CurrentCulture, LocalizableStrings.TestDiscoverySummary, totalTests, assemblies.Count));
        }

        terminal.ResetColor();
        terminal.AppendLine();

        if (_wasCancelled)
        {
            terminal.Append(LocalizableStrings.Aborted);
            terminal.AppendLine();
        }
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
        string testNodeUid,
        string displayName,
        string? executionId)
    {
        TestProgressState asm = _assemblies[$"{assembly}|{targetFramework}|{architecture}|{executionId}"];

        if (_options.ShowActiveTests)
        {
            asm.TestNodeResultsState ??= new(Interlocked.Increment(ref _counter));
            asm.TestNodeResultsState.AddRunningTestNode(
                Interlocked.Increment(ref _counter), testNodeUid, displayName, CreateStopwatch());
        }

        _terminalWithProgress.UpdateWorker(asm.SlotIndex);
    }
}
