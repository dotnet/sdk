// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Globalization;

namespace Microsoft.DotNet.Cli.Commands.Test.Terminal;

internal sealed partial class TerminalTestReporter
{
    private readonly Lock _collectionLock = new();
    private ITimer? _collectionTimer;
    private int _collectionModuleCount;
    private int _collectionFinishedModules;
    private string? _collectionActiveModule;
    private long _collectionLastProgressTimestamp;
    private bool _collectionFinished;
    private int _artifactPostProcessingFailures;
    private readonly ConcurrentQueue<string> _deferredArtifactPostProcessingFailures = new();

    internal void TestMapCollectionStarted(int moduleCount)
    {
        if (!_options.CollectTestMap || _isHelp || _isDiscovery)
        {
            return;
        }

        _collectionModuleCount = moduleCount;
        _collectionLastProgressTimestamp = _timeProvider.GetTimestamp();
        WriteInformationMessage(string.Format(CultureInfo.CurrentCulture, CliCommandStrings.TestMapCollectionStarted, moduleCount));
        if (_options.ShowProgress && _options.HeartbeatSilenceThreshold > TimeSpan.Zero)
        {
            _collectionTimer = _timeProvider.CreateTimer(
                _ => WriteTestMapCollectionProgress(), null,
                TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }
    }

    internal void TestMapCollectionModuleStarted(string module)
    {
        if (_options.CollectTestMap)
        {
            lock (_collectionLock)
            {
                _collectionActiveModule = module;
            }
        }
    }

    internal void TestMapCollectionModuleCompleted()
    {
        if (_options.CollectTestMap)
        {
            lock (_collectionLock)
            {
                _collectionFinishedModules++;
                _collectionActiveModule = null;
            }
        }
    }

    private void WriteTestMapCollectionProgress()
    {
        lock (_collectionLock)
        {
            long timestamp = _timeProvider.GetTimestamp();
            if (_collectionFinished || _wasCancelled ||
                _timeProvider.GetElapsedTime(_collectionLastProgressTimestamp, timestamp) < _options.HeartbeatSilenceThreshold)
            {
                return;
            }

            _collectionLastProgressTimestamp = timestamp;
            WriteInformationMessage(string.Format(
                CultureInfo.CurrentCulture, CliCommandStrings.TestMapCollectionProgress,
                _collectionFinishedModules, _collectionModuleCount,
                _collectionActiveModule ?? CliCommandStrings.TestMapCollectionWaiting));
        }
    }

    private void StopTestMapCollectionProgress()
    {
        lock (_collectionLock)
        {
            _collectionFinished = true;
        }

        Interlocked.Exchange(ref _collectionTimer, null)?.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private void AppendTestMapCollectionSummary(ITerminal terminal, int? exitCode)
    {
        terminal.AppendLine();
        terminal.SetColor(exitCode == ExitCode.Success ? TerminalColor.Default : TerminalColor.DarkRed);
        terminal.AppendLine(_wasCancelled
            ? CliCommandStrings.TestMapCollectionAborted
            : exitCode == ExitCode.Success
                ? CliCommandStrings.TestMapCollectionCompleted
                : CliCommandStrings.TestMapCollectionFailed);
        terminal.ResetColor();
        terminal.AppendLine(string.Format(
            CultureInfo.CurrentCulture, CliCommandStrings.TestMapCollectionModules,
            _collectionFinishedModules, _collectionModuleCount));
        terminal.AppendLine(CliCommandStrings.TestMapCollectionNoTestVerdict);
        terminal.Append($"{SingleIndentation}{CliCommandStrings.DurationColon} ");
        AppendLongDuration(terminal, (_testExecutionEndTime - _testExecutionStartTime) ?? TimeSpan.Zero, wrapInParentheses: false, colorize: false);
        terminal.AppendLine();
        AppendHandshakeFailureRecap(terminal);
    }

    internal void ArtifactPostProcessingFailed(string message)
    {
        Interlocked.Increment(ref _artifactPostProcessingFailures);
        WriteWarningMessage(message);
    }

    internal void DeferArtifactPostProcessingFailure(string message)
    {
        Interlocked.Increment(ref _artifactPostProcessingFailures);
        _deferredArtifactPostProcessingFailures.Enqueue(message);
    }

    private void AppendReportingStatus(ITerminal terminal, IEnumerable<TestProgressState> assemblies)
    {
        if (_artifactPostProcessingFailures > 0)
        {
            terminal.SetColor(TerminalColor.DarkYellow);
            terminal.AppendLine(string.Format(
                CultureInfo.CurrentCulture, CliCommandStrings.ArtifactReportingDegraded,
                _artifactPostProcessingFailures));
            foreach (string message in _deferredArtifactPostProcessingFailures)
            {
                terminal.AppendLine(message);
            }

            terminal.ResetColor();
        }

        foreach (TestProgressState assembly in assemblies.Where(assembly => assembly.HasIncompleteTestResults))
        {
            terminal.SetColor(TerminalColor.DarkYellow);
            terminal.AppendLine(string.Format(
                CultureInfo.CurrentCulture, CliCommandStrings.IncompleteLiveTestResults,
                assembly.Assembly, assembly.TotalTests));
            terminal.ResetColor();
        }
    }
}
