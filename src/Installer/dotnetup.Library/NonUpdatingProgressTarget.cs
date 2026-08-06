// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper;

public class NonUpdatingProgressTarget : IProgressTarget
{
    public IProgressReporter CreateProgressReporter() => new Reporter();

    private sealed class Reporter : IProgressReporter
    {
        private readonly object _consoleLock = new();

        public IProgressTask AddTask(string description, double maxValue)
        {
            var task = new ProgressTaskImpl(description, _consoleLock) { MaxValue = maxValue };
            lock (_consoleLock)
            {
                AnsiConsole.WriteLine(description + "...");
            }
            return task;
        }

        public void Dispose()
        {
        }
    }

    private sealed class ProgressTaskImpl : IProgressTask
    {
#pragma warning disable IDE0032 // Setter has side-effect logic; not convertible to auto-property
        private double _value;
#pragma warning restore IDE0032
        private int _completed;
        private readonly object _consoleLock;

        public ProgressTaskImpl(string description, object consoleLock)
        {
            Description = description;
            _consoleLock = consoleLock;
        }

        public double Value
        {
            get => _value;
            set
            {
                _value = value;
                if (_value >= MaxValue && Interlocked.CompareExchange(ref _completed, 1, 0) == 0)
                {
                    lock (_consoleLock)
                    {
                        AnsiConsole.MarkupLine($"{DotnetupTheme.Brand("Completed:")} {Description}");
                    }
                }
            }
        }

        public string Description { get; set; }
        public double MaxValue { get; set; }
    }
}
