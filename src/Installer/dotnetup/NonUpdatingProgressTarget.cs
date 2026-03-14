// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper;

public class NonUpdatingProgressTarget : IProgressTarget
{
    public IProgressReporter CreateProgressReporter() => new Reporter();

    private sealed class Reporter : IProgressReporter
    {
        public IProgressTask AddTask(string description, double maxValue)
        {
            var task = new ProgressTaskImpl(description) { MaxValue = maxValue };
            AnsiConsole.WriteLine(description + "...");
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
        private bool _completed;

        public ProgressTaskImpl(string description)
        {
            Description = description;
        }

        public double Value
        {
            get => _value;
            set
            {
                _value = value;
                if (_value >= MaxValue && !_completed)
                {
                    _completed = true;
                    AnsiConsole.MarkupLine($"{DotnetupTheme.Brand("Completed:")} {Description}");
                }
            }
        }

        public string Description { get; set; }
        public double MaxValue { get; set; }
    }
}
