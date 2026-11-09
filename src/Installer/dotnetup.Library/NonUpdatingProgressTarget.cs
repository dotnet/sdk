// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Writes one line when a task starts and one when it finishes, for redirected or non-interactive output.
/// </summary>
/// <remarks>
/// Task descriptions are padded for the column layout of <see cref="SpectreProgressTarget"/>, so the
/// padding is collapsed here. A task's finished line is the description it is given after reaching its
/// maximum (for example "Installed ..."); tasks that never get one report "Completed: ..." on dispose.
/// </remarks>
public class NonUpdatingProgressTarget : IProgressTarget
{
    public IProgressReporter CreateProgressReporter() => new Reporter();

    private static string CollapseAlignmentPadding(string description) =>
        string.Join(' ', description.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Replace("( ", "(", StringComparison.Ordinal);

    private sealed class Reporter : IProgressReporter
    {
        private readonly object _consoleLock = new();
        private readonly List<ProgressTaskImpl> _tasks = [];

        public IProgressTask AddTask(string description, double maxValue)
        {
            var task = new ProgressTaskImpl(description, _consoleLock) { MaxValue = maxValue };
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
        private string _description;
        private double _value;
        private bool _completed;
        private bool _reported;

        public ProgressTaskImpl(string description, object consoleLock)
        {
            _description = description;
            _startDescription = CollapseAlignmentPadding(description);
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
        public bool RequiresKnownMaximum => false;

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
