// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Writes plain progress lines for redirected or non-interactive output: one when a task starts, one when it
/// finishes, and, when <c>progressStepPercent</c> is positive, one each time a task crosses another step.
/// </summary>
/// <remarks>
/// Task descriptions are padded for the column layout of <see cref="SpectreProgressTarget"/>, so the
/// padding is collapsed here. A task's finished line is the description it is given after reaching its
/// maximum (for example "Installed ..."); tasks that never get one report "Completed: ..." on dispose.
/// </remarks>
public class NonUpdatingProgressTarget(int progressStepPercent = 0) : IProgressTarget
{
    public IProgressReporter CreateProgressReporter() => new Reporter(progressStepPercent);

    private static string CollapseAlignmentPadding(string description) =>
        string.Join(' ', description.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Replace("( ", "(", StringComparison.Ordinal);

    private sealed class Reporter(int progressStepPercent) : IProgressReporter
    {
        private readonly object _consoleLock = new();
        private readonly List<ProgressTaskImpl> _tasks = [];

        public IProgressTask AddTask(string description, double maxValue)
        {
            var task = new ProgressTaskImpl(description, progressStepPercent, _consoleLock) { MaxValue = maxValue };
            lock (_consoleLock)
            {
                _tasks.Add(task);
                AnsiConsole.WriteLine(CollapseAlignmentPadding(description) + "...");
            }
            return task;
        }

        public void Dispose()
        {
            lock (_consoleLock)
            {
                foreach (var task in _tasks)
                {
                    task.ReportCompletedIfUnreported();
                }
            }
        }
    }

    private sealed class ProgressTaskImpl : IProgressTask
    {
        private readonly object _consoleLock;
        private readonly string _startDescription;
        private readonly int _progressStepPercent;
        private string _description;
        private double _value;
        private int _lastReportedPercent;
        private bool _completed;
        private bool _reported;

        public ProgressTaskImpl(string description, int progressStepPercent, object consoleLock)
        {
            _description = description;
            _startDescription = CollapseAlignmentPadding(description);
            _progressStepPercent = progressStepPercent;
            _consoleLock = consoleLock;
        }

        public double Value
        {
            get => _value;
            set
            {
                lock (_consoleLock)
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
                lock (_consoleLock)
                {
                    _description = value;
                    string collapsed = CollapseAlignmentPadding(value);
                    // Byte-count updates keep the start text as a prefix; a new label marks the finished state.
                    if (_completed && !_reported && !collapsed.StartsWith(_startDescription, StringComparison.Ordinal))
                    {
                        _reported = true;
                        AnsiConsole.WriteLine(collapsed);
                    }
                }
            }
        }

        public double MaxValue { get; set; }
        public bool RequiresKnownMaximum => _progressStepPercent > 0;

        private void ReportStepIfCrossed()
        {
            if (_progressStepPercent <= 0 || _completed || MaxValue <= 0)
            {
                return;
            }

            int percent = (int)(_value / MaxValue * 100) / _progressStepPercent * _progressStepPercent;
            if (percent <= _lastReportedPercent)
            {
                return;
            }

            _lastReportedPercent = percent;
            string current = CollapseAlignmentPadding(_description);
            string detail = current.StartsWith(_startDescription, StringComparison.Ordinal) ? current[_startDescription.Length..] : string.Empty;
            AnsiConsole.WriteLine(FormattableString.Invariant($"{_startDescription}: {percent}%{detail}"));
        }

        public void ReportCompletedIfUnreported()
        {
            if (_completed && !_reported)
            {
                _reported = true;
                AnsiConsole.MarkupLine($"{DotnetupTheme.Brand("Completed:")} {_startDescription}");
            }
        }
    }
}
