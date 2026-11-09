// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Tracks when each progress task starts, crosses a percentage step, and finishes, for outputs that can
/// only append (logs, pipes, event streams) rather than redraw.
/// </summary>
/// <remarks>
/// Task descriptions are padded for the column layout of <see cref="SpectreProgressTarget"/>, so the
/// padding is collapsed before it is written. A task's finished description is the one it is given after
/// reaching its maximum (for example "Installed ..."); tasks that never get one finish on dispose with
/// their start description.
/// </remarks>
public abstract class AppendOnlyProgressTarget(int progressStepPercent) : IProgressTarget
{
    public IProgressReporter CreateProgressReporter() => new Reporter(this);

    private int ProgressStepPercent { get; } = progressStepPercent;

    /// <summary>Writes that a task started.</summary>
    protected abstract void WriteStarted(int taskId, string description);

    /// <summary>Writes that a task crossed a step; <paramref name="currentDescription"/> may carry details such as byte counts.</summary>
    protected abstract void WriteStep(int taskId, string startDescription, string currentDescription, int percent);

    /// <summary>
    /// Writes that a task finished. <paramref name="hasFinishedDescription"/> is false when the task never
    /// received a finished description and <paramref name="description"/> is its start description.
    /// </summary>
    protected abstract void WriteFinished(int taskId, string description, bool hasFinishedDescription);

    private static string CollapseAlignmentPadding(string description) =>
        string.Join(' ', description.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Replace("( ", "(", StringComparison.Ordinal);

    private sealed class Reporter(AppendOnlyProgressTarget target) : IProgressReporter
    {
        private readonly object _outputLock = new();
        private readonly List<ProgressTaskImpl> _tasks = [];

        public IProgressTask AddTask(string description, double maxValue)
        {
            lock (_outputLock)
            {
                var task = new ProgressTaskImpl(target, _tasks.Count + 1, description, _outputLock) { MaxValue = maxValue };
                _tasks.Add(task);
                target.WriteStarted(task.Id, CollapseAlignmentPadding(description));
                return task;
            }
        }

        public void Dispose()
        {
            lock (_outputLock)
            {
                foreach (var task in _tasks)
                {
                    task.FinishIfUnreported();
                }
            }
        }
    }

    private sealed class ProgressTaskImpl(AppendOnlyProgressTarget target, int id, string description, object outputLock) : IProgressTask
    {
        private readonly string _startDescription = CollapseAlignmentPadding(description);
        private string _description = description;
        private double _value;
        private int _lastReportedPercent;
        private bool _completed;
        private bool _reported;

        public int Id => id;

        public double Value
        {
            get => _value;
            set
            {
                lock (outputLock)
                {
                    _value = value;
                    _completed |= _value >= MaxValue;
                    ReportStepIfCrossed();
                }
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                lock (outputLock)
                {
                    _description = value;
                    string collapsed = CollapseAlignmentPadding(value);
                    // Byte-count updates keep the start text as a prefix; a new label marks the finished state.
                    if (_completed && !_reported && !collapsed.StartsWith(_startDescription, StringComparison.Ordinal))
                    {
                        _reported = true;
                        target.WriteFinished(id, collapsed, hasFinishedDescription: true);
                    }
                }
            }
        }

        public double MaxValue { get; set; }
        public bool RequiresKnownMaximum => target.ProgressStepPercent > 0;

        public void FinishIfUnreported()
        {
            if (_completed && !_reported)
            {
                _reported = true;
                target.WriteFinished(id, _startDescription, hasFinishedDescription: false);
            }
        }

        private void ReportStepIfCrossed()
        {
            int step = target.ProgressStepPercent;
            if (step <= 0 || _completed || MaxValue <= 0)
            {
                return;
            }

            int percent = (int)(_value / MaxValue * 100) / step * step;
            if (percent <= _lastReportedPercent)
            {
                return;
            }

            _lastReportedPercent = percent;
            target.WriteStep(id, _startDescription, CollapseAlignmentPadding(_description), percent);
        }
    }
}
