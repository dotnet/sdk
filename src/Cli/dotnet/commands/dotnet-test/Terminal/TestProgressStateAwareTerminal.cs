// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

/// <summary>
/// Terminal that updates the progress in place when progress reporting is enabled.
/// </summary>
internal sealed partial class TestProgressStateAwareTerminal : IDisposable
{
    /// <summary>
    /// A cancellation token to signal the rendering thread that it should exit.
    /// </summary>
    private readonly CancellationTokenSource _cts = new();

    /// <summary>
    /// Protects access to state shared between the logger callbacks and the rendering thread.
    /// </summary>
    private readonly Lock _lock = new();

    private readonly ITerminal _terminal;
    private readonly Func<bool?> _showProgress;
    private readonly bool _writeProgressImmediatelyAfterOutput;
    private readonly int _updateEvery;
    private TestProgressState?[] _progressItems = [];
    private bool? _showProgressCached;

    /// <summary>
    /// The thread that performs periodic refresh of the console output.
    /// </summary>
    private Thread? _refresher;
    private long _counter;

    public event EventHandler? OnProgressStartUpdate;

    public event EventHandler? OnProgressStopUpdate;

    /// <summary>
    /// The <see cref="_refresher"/> thread proc.
    /// </summary>
    private void ThreadProc()
    {
        try
        {
            while (!_cts.Token.WaitHandle.WaitOne(_updateEvery))
            {
                lock (_lock)
                {
                    OnProgressStartUpdate?.Invoke(this, EventArgs.Empty);
                    _terminal.StartUpdate();
                    try
                    {
                        _terminal.RenderProgress(_progressItems);
                    }
                    finally
                    {
                        _terminal.StopUpdate();
                        OnProgressStopUpdate?.Invoke(this, EventArgs.Empty);
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

    public TestProgressStateAwareTerminal(ITerminal terminal, Func<bool?> showProgress, bool writeProgressImmediatelyAfterOutput, int updateEvery)
    {
        _terminal = terminal;
        _showProgress = showProgress;
        _writeProgressImmediatelyAfterOutput = writeProgressImmediatelyAfterOutput;
        _updateEvery = updateEvery;
    }

    public int AddWorker(TestProgressState testWorker)
    {
        if (GetShowProgress())
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
        if (GetShowProgress())
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
        if (GetShowProgress())
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
        if (GetShowProgress())
        {
            lock (_lock)
            {
                try
                {
                    _terminal.StartUpdate();
                    _terminal.EraseProgress();
                    write(_terminal);
                    if (_writeProgressImmediatelyAfterOutput)
                    {
                        _terminal.RenderProgress(_progressItems);
                    }
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
        if (GetShowProgress())
        {
            _progressItems[slotIndex] = null;
        }
    }

    internal void UpdateWorker(int slotIndex)
    {
        if (GetShowProgress())
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

    private bool GetShowProgress()
    {
        if (_showProgressCached != null)
        {
            return _showProgressCached.Value;
        }

        // Get the value from the func until we get the first non-null value.
        bool? showProgress = _showProgress();
        if (showProgress != null)
        {
            _showProgressCached = showProgress;
        }

        return showProgress == true;
    }
}
