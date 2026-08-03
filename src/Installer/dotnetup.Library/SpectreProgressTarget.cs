// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper;

public class SpectreProgressTarget : IProgressTarget
{
    public IProgressReporter CreateProgressReporter() => new Reporter();

    private sealed class Reporter : IProgressReporter
    {
        private readonly TaskCompletionSource _overallTask = new();
        private readonly ProgressContext _progressContext;
        private readonly List<ShimmerProgressTask> _tasks = [];

        public Reporter()
        {
            TaskCompletionSource<ProgressContext> tcs = new();
            var progress = AnsiConsole.Progress();
            var successAltStyle = Style.Parse(DotnetupTheme.Current.SuccessAlt);
            progress.Columns(
                new SpinnerColumn(Spinner.Known.Line) { Style = Style.Parse(DotnetupTheme.Current.Brand) },
                new TaskDescriptionColumn { Alignment = Justify.Left },
                new ProgressBarColumn
                {
                    CompletedStyle = Style.Parse(DotnetupTheme.Current.Brand),
                    FinishedStyle = successAltStyle,
                    RemainingStyle = new Style(Color.Grey),
                },
                new PercentageColumn
                {
                    CompletedStyle = successAltStyle,
                });

            var progressTask = progress.StartAsync(async ctx =>
            {
                tcs.SetResult(ctx);
                await _overallTask.Task.ConfigureAwait(false);
            });

            _progressContext = tcs.Task.GetAwaiter().GetResult();
        }

        public IProgressTask AddTask(string description, double maxValue)
        {
            var adapter = new SpectreProgressTaskAdapter(_progressContext.AddTask(description, maxValue: maxValue));
            var task = new ShimmerProgressTask(adapter);
            _tasks.Add(task);
            return task;
        }

        public void Dispose()
        {
            foreach (var task in _tasks)
            {
                task.StopShimmer();
            }

            _overallTask.TrySetResult();
        }
    }

    /// <summary>
    /// Adapter that makes <see cref="Spectre.Console.ProgressTask"/> usable as a plain
    /// <see cref="IProgressTask"/> backing store for <see cref="ShimmerProgressTask"/>.
    /// </summary>
    private sealed class SpectreProgressTaskAdapter(Spectre.Console.ProgressTask task) : IProgressTask
    {
        public string Description { get => task.Description; set => task.Description = value; }
        public double Value { get => task.Value; set => task.Value = value; }
        public double MaxValue { get => task.MaxValue; set => task.MaxValue = value; }
    }
}
