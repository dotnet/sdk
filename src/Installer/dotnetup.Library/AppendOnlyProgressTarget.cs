// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Tracks when each progress task starts, crosses a percentage step, and finishes, for outputs that can
/// only append (logs, pipes, event streams) rather than redraw.
/// </summary>
/// <remarks>
/// Task descriptions are padded for the column layout of <see cref="SpectreProgressTarget"/>, so the
/// padding is collapsed before it is written. A task finishes only when it is given a finished description
/// after reaching its maximum (for example "Installed ..."), so work that fails after reaching its maximum
/// is never reported as finished.
/// </remarks>
public abstract class AppendOnlyProgressTarget(int progressStepPercent, ProgressTaskIds? taskIds = null) : IProgressTarget
{
    public IProgressReporter CreateProgressReporter() => new Reporter(this);

    private int ProgressStepPercent { get; } = progressStepPercent;

    private ProgressTaskIds TaskIds { get; } = taskIds ?? new ProgressTaskIds();

    /// <summary>Writes that a task started.</summary>
    protected abstract void WriteStarted(int taskId, string description);

    /// <summary>Writes that a task crossed a step; <paramref name="currentDescription"/> may carry details such as byte counts.</summary>
    protected abstract void WriteStep(int taskId, string startDescription, string currentDescription, int percent);

    /// <summary>Writes that a task finished with its finished description.</summary>
    protected abstract void WriteFinished(int taskId, string description);

    private static string CollapseAlignmentPadding(string description) =>
        string.Join(' ', description.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Replace("( ", "(", StringComparison.Ordinal);

    private sealed class Reporter(AppendOnlyProgressTarget target) : IProgressReporter
    {
        private readonly object _outputLock = new();

        public IProgressTask AddTask(string description, double maxValue)
        {
            lock (_outputLock)
            {
                var task = new ProgressTaskImpl(target, target.TaskIds.Next(), description, _outputLock) { MaxValue = maxValue };
                target.WriteStarted(task.Id, CollapseAlignmentPadding(description));
                return task;
            }
        }

        public void Dispose()
        {
        }
    }

    private sealed class ProgressTaskImpl(AppendOnlyProgressTarget target, int id, string description, object outputLock) : IProgressTask
    {
        private readonly string _startDescription = CollapseAlignmentPadding(description);
        private int _lastReportedPercent;
        private bool _completed;
        private bool _reported;

        public int Id => id;

        public double Value
        {
            get;
            set
            {
                lock (outputLock)
                {
                    field = value;
                    _completed |= field >= MaxValue;
                    ReportStepIfCrossed();
                }
            }
        }

        public string Description
        {
            get;
            set
            {
                lock (outputLock)
                {
                    field = value;
                    string collapsed = CollapseAlignmentPadding(value);
                    // Byte-count updates keep the start text as a prefix; a new label marks the finished state.
                    if (_completed && !_reported && !collapsed.StartsWith(_startDescription, StringComparison.Ordinal))
                    {
                        _reported = true;
                        target.WriteFinished(id, collapsed);
                    }
                }
            }
        } = description;

        public double MaxValue { get; set; }
        public bool RequiresKnownMaximum => target.ProgressStepPercent > 0;

        private void ReportStepIfCrossed()
        {
            int step = target.ProgressStepPercent;
            if (step <= 0 || _completed || MaxValue <= 0)
            {
                return;
            }

            int percent = (int)(Value / MaxValue * 100) / step * step;
            if (percent <= _lastReportedPercent)
            {
                return;
            }

            _lastReportedPercent = percent;
            target.WriteStep(id, _startDescription, CollapseAlignmentPadding(Description), percent);
        }
    }
}
