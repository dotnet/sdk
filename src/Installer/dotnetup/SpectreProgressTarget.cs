// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper;

public class SpectreProgressTarget : IProgressTarget
{
    public IProgressReporter CreateProgressReporter() => new Reporter();

    class Reporter : IProgressReporter
    {
        TaskCompletionSource _overallTask = new TaskCompletionSource();
        ProgressContext _progressContext;

        public Reporter()
        {
            TaskCompletionSource<ProgressContext> tcs = new TaskCompletionSource<ProgressContext>();
            var progressTask = AnsiConsole.Progress().StartAsync(async ctx =>
            {
                tcs.SetResult(ctx);
                await _overallTask.Task;
            });

            _progressContext = tcs.Task.GetAwaiter().GetResult();
        }

        public IProgressTask AddTask(string description, double maxValue)
        {
            return new ProgressTaskImpl(_progressContext.AddTask(description, maxValue: maxValue));
        }

        public void Dispose()
        {
            _overallTask.SetResult();
        }
    }

    class ProgressTaskImpl : IProgressTask
    {
        private readonly Spectre.Console.ProgressTask _task;
        public ProgressTaskImpl(Spectre.Console.ProgressTask task)
        {
            _task = task;
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
    }
}
