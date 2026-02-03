// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Test.Terminal;

/// <summary>
/// Terminal that updates the progress in place when progress reporting is enabled.
/// </summary>
internal sealed partial class TestProgressStateAwareTerminal(ITerminal terminal, bool showProgress) : IDisposable
{
    /// <summary>
    /// A cancellation token to signal the rendering thread that it should exit.
    /// </summary>
    private readonly CancellationTokenSource _cts = new();

    /// <summary>
    /// Protects access to state shared between the logger callbacks and the rendering thread.
    /// </summary>
    private readonly object _lock = Console.Out;

    private readonly ITerminal _terminal = terminal;
    private readonly bool _showProgress = showProgress;
    private TestProgressState?[] _progressItems = [];

    /// <summary>
    /// The thread that performs periodic refresh of the console output.
    /// </summary>
    private Thread? _refresher;
    private long _counter;

    /// <summary>
    /// The <see cref="_refresher"/> thread proc.
    /// </summary>
    private void ThreadProc()
    {
        try
        {
            // When writing to ANSI, we update the progress in place and it should look responsive so we
            // update every half second, because we only show seconds on the screen, so it is good enough.
            // When writing to non-ANSI, we never show progress as the output can get long and messy.
            const int AnsiUpdateCadenceInMs = 500;
            while (!_cts.Token.WaitHandle.WaitOne(AnsiUpdateCadenceInMs))
            {
                lock (_lock)
                {
                    _terminal.StartUpdate();
                    try
                    {
                        _terminal.RenderProgress(_progressItems);
                    }
                    finally
                    {
                        _terminal.StopUpdate();
                    }
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // When we dispose _cts too early this will throw.
        }

        _terminal.EraseProgress();
    }

    public int AddWorker(TestProgressState testWorker)
    {
        if (_showProgress)
        {
            for (int i = 0; i < _progressItems.Length; i++)
            {
                if (_progressItems[i] == null)
                {
                    _progressItems[i] = testWorker;
                    return i;
                }
            }

            throw new InvalidOperationException("No empty slot found");
        }

        return 0;
    }

    public void StartShowingProgress(int workerCount)
    {
        if (_showProgress)
        {
            _progressItems = new TestProgressState[workerCount];
            _terminal.StartBusyIndicator();
            // If we crash unexpectedly without completing this thread we don't want it to keep the process running.
            _refresher = new Thread(ThreadProc) { IsBackground = true };
            _refresher.Start();
        }
    }

    internal void StopShowingProgress()
    {
        if (_showProgress)
        {
            _cts.Cancel();
            _refresher?.Join();

            _terminal.EraseProgress();
            _terminal.StopBusyIndicator();
        }
    }

    public void Dispose() =>
        ((IDisposable)_cts).Dispose();

    internal void WriteToTerminal(Action<ITerminal> write)
    {
        if (_showProgress)
        {
            lock (_lock)
            {
                try
                {
                    _terminal.StartUpdate();
                    _terminal.EraseProgress();
                    write(_terminal);
                    _terminal.RenderProgress(_progressItems);
                }
                finally
                {
                    _terminal.StopUpdate();
                }
            }
        }
        else
        {
            lock (_lock)
            {
                try
                {
                    _terminal.StartUpdate();
                    write(_terminal);
                }
                finally
                {
                    _terminal.StopUpdate();
                }
            }
        }
    }

    internal void RemoveWorker(int slotIndex)
    {
        if (_showProgress)
        {
            _progressItems[slotIndex] = null;
        }
    }

    internal void UpdateWorker(int slotIndex)
    {
        if (_showProgress)
        {
            // We increase the counter to say that this version of data is newer than what we had before and
            // it should be completely re-rendered. Another approach would be to use timestamps, or to replace the
            // instance and compare that, but that means more objects floating around.
            _counter++;

            TestProgressState? progress = _progressItems[slotIndex];
            if (progress != null)
            {
                progress.Version = _counter;
            }
        }
    }
}
