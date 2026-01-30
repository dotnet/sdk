// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;
using OpenTelemetry.Trace;
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
            // Tag library activities so consumers know they came from dotnetup CLI
            activity?.SetTag("caller", "dotnetup");
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
            
            // Use ErrorCodeMapper for rich error metadata (same as command-level telemetry)
            var errorInfo = ErrorCodeMapper.GetErrorInfo(ex);
            
            _activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            _activity.SetTag("error.type", errorInfo.ErrorType);
            
            if (errorInfo.StatusCode.HasValue)
            {
                _activity.SetTag("error.http_status", errorInfo.StatusCode.Value);
            }

            if (errorInfo.HResult.HasValue)
            {
                _activity.SetTag("error.hresult", errorInfo.HResult.Value);
            }

            if (errorInfo.Details is not null)
            {
                _activity.SetTag("error.details", errorInfo.Details);
            }

            if (errorInfo.SourceLocation is not null)
            {
                _activity.SetTag("error.source_location", errorInfo.SourceLocation);
            }

            if (errorInfo.ExceptionChain is not null)
            {
                _activity.SetTag("error.exception_chain", errorInfo.ExceptionChain);
            }

            _activity.RecordException(ex);
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
