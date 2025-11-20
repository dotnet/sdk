// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;
using System.Text;

namespace Microsoft.DotNet.Tools.Bootstrapper;

public class NonUpdatingProgressTarget : IProgressTarget
{
    public IProgressReporter CreateProgressReporter() => new Reporter();

    class Reporter : IProgressReporter
    {
        List<ProgressTaskImpl> _tasks = new();

        public IProgressTask AddTask(string description, double maxValue)
        {
            var task = new ProgressTaskImpl(description)
            {
                MaxValue = maxValue
            };
            _tasks.Add(task);
            Spectre.Console.AnsiConsole.WriteLine(description + "...");
            return task;
        }
        public void Dispose()
        {
            foreach (var task in _tasks)
            {
                task.Complete();
            }
        }
    }

    class ProgressTaskImpl : IProgressTask
    {
        bool _completed = false;
        double _value;

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
                if (_value >= MaxValue)
                {
                    Complete();
                }
            }
        }
        public string Description { get; set; }
        public double MaxValue { get; set; }

        public void Complete()
        {
            if (!_completed)
            {
                Spectre.Console.AnsiConsole.MarkupLine($"[green]Completed:[/] {Description}");
                _completed = true;
            }
        }
    }
}
