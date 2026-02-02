// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;
using OpenTelemetry.Trace;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper;

public class SpectreProgressTarget : IProgressTarget
{
    public IProgressReporter CreateProgressReporter() => new Reporter();

    private sealed class Reporter : IProgressReporter
    {
        private readonly TaskCompletionSource _overallTask = new();
        private readonly ProgressContext _progressContext;
        private readonly List<ProgressTaskImpl> _tasks = new();

        public Reporter()
        {
            TaskCompletionSource<ProgressContext> tcs = new();
            var progressTask = AnsiConsole.Progress().StartAsync(async ctx =>
            {
                tcs.SetResult(ctx);
                await _overallTask.Task;
            });

            _progressContext = tcs.Task.GetAwaiter().GetResult();
        }

        public IProgressTask AddTask(string description, double maxValue)
        {
            var task = new ProgressTaskImpl(_progressContext.AddTask(description, maxValue: maxValue), activity: null);
            _tasks.Add(task);
            return task;
        }

        public IProgressTask AddTask(string activityName, string description, double maxValue)
        {
            var activity = InstallationActivitySource.ActivitySource.StartActivity(activityName, ActivityKind.Internal);
            // Tag library activities so consumers know they came from dotnetup CLI
            activity?.SetTag("caller", "dotnetup");
            var task = new ProgressTaskImpl(_progressContext.AddTask(description, maxValue: maxValue), activity);
            _tasks.Add(task);
            return task;
        }

        public void Dispose()
        {
            foreach (var task in _tasks)
            {
                task.DisposeActivity();
            }
            _overallTask.SetResult();
        }
    }

    private sealed class ProgressTaskImpl : IProgressTask
    {
        private readonly Spectre.Console.ProgressTask _task;
        private readonly Activity? _activity;
        private bool _completed;

        public ProgressTaskImpl(Spectre.Console.ProgressTask task, Activity? activity)
        {
            _task = task;
            _activity = activity;
        }

        public double Value
        {
            get => _task.Value;
            set => _task.Value = value;
        }

        public string Description
        {
            get => _task.Description;
            set => _task.Description = value;
        }

        public double MaxValue
        {
            get => _task.MaxValue;
            set => _task.MaxValue = value;
        }

        public void SetTag(string key, object? value)
        {
            if (_activity == null) return;

            // Sanitize URL tags to prevent PII leakage
            if (key == "download.url" && value is string url)
            {
                _activity.SetTag("download.url_domain", UrlSanitizer.SanitizeDomain(url));
                return;
            }

            _activity.SetTag(key, value);
        }

        public void RecordError(Exception ex)
        {
            if (_activity == null) return;

            // Use ErrorCodeMapper for rich error metadata (same as command-level telemetry)
            var errorInfo = ErrorCodeMapper.GetErrorInfo(ex);
            ErrorCodeMapper.ApplyErrorTags(_activity, errorInfo);
        }

        public void Complete()
        {
            if (_completed) return;
            _completed = true;
            _activity?.SetStatus(ActivityStatusCode.Ok);
        }

        public void DisposeActivity()
        {
            // Ensure Spectre task shows as complete (visually)
            _task.Value = _task.MaxValue;

            if (!_completed)
            {
                _activity?.SetStatus(ActivityStatusCode.Unset);
            }
            _activity?.Dispose();
        }
    }
}
