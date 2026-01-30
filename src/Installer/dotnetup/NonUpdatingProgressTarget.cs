// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Dotnet.Installation.Internal;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper;

public class NonUpdatingProgressTarget : IProgressTarget
{
    public IProgressReporter CreateProgressReporter() => new Reporter();

    private sealed class Reporter : IProgressReporter
    {
        private readonly List<ProgressTaskImpl> _tasks = new();

        public IProgressTask AddTask(string description, double maxValue)
        {
            var task = new ProgressTaskImpl(description, activity: null) { MaxValue = maxValue };
            _tasks.Add(task);
            AnsiConsole.WriteLine(description + "...");
            return task;
        }

        public IProgressTask AddTask(string activityName, string description, double maxValue)
        {
            var activity = InstallationActivitySource.ActivitySource.StartActivity(activityName, ActivityKind.Internal);
            var task = new ProgressTaskImpl(description, activity) { MaxValue = maxValue };
            _tasks.Add(task);
            AnsiConsole.WriteLine(description + "...");
            return task;
        }

        public void Dispose()
        {
            foreach (var task in _tasks)
            {
                task.Complete();
                task.DisposeActivity();
            }
        }
    }

    private sealed class ProgressTaskImpl : IProgressTask
    {
        private readonly Activity? _activity;
        private bool _completed;
        private double _value;

        public ProgressTaskImpl(string description, Activity? activity)
        {
            Description = description;
            _activity = activity;
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

        public void SetTag(string key, object? value) => _activity?.SetTag(key, value);

        public void RecordError(Exception ex)
        {
            if (_activity == null) return;
            _activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            _activity.SetTag("error.type", ex.GetType().Name);
            _activity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
            {
                { "exception.type", ex.GetType().FullName },
                { "exception.message", ex.Message }
            }));
        }

        public void Complete()
        {
            if (_completed) return;
            _completed = true;
            _activity?.SetStatus(ActivityStatusCode.Ok);
            AnsiConsole.MarkupLine($"[green]Completed:[/] {Description}");
        }

        public void DisposeActivity()
        {
            // Don't print "Completed" again if already completed
            if (!_completed)
            {
                _activity?.SetStatus(ActivityStatusCode.Unset);
            }
            _activity?.Dispose();
        }
    }
}
