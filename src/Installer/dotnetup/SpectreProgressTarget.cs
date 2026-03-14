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

        public Reporter()
        {
            TaskCompletionSource<ProgressContext> tcs = new();
            var progressTask = AnsiConsole.Progress().StartAsync(async ctx =>
            {
                tcs.SetResult(ctx);
                await _overallTask.Task.ConfigureAwait(false);
            });

            _progressContext = tcs.Task.GetAwaiter().GetResult();
        }

        public IProgressTask AddTask(string description, double maxValue)
        {
            return new ProgressTaskImpl(_progressContext.AddTask(description, maxValue: maxValue));
        }

        public void Dispose()
        {
            _overallTask.TrySetResult();
        }
    }

#pragma warning disable CA1001 // Timer is disposed in StopShimmer when the task completes
    private sealed class ProgressTaskImpl : IProgressTask
#pragma warning restore CA1001
    {
        private readonly Spectre.Console.ProgressTask _task;
        private readonly string _baseDescription;
        private readonly string? _shimmerWord;
        private readonly string? _restEscaped;
        private readonly Timer? _shimmerTimer;
        private int _shimmerTick;
        private volatile bool _shimmerStopped;

        public ProgressTaskImpl(Spectre.Console.ProgressTask task)
        {
            _task = task;
            _baseDescription = task.Description;

            int spaceIndex = _baseDescription.IndexOf(' ');
            if (spaceIndex > 0 && _baseDescription.StartsWith("Installing", StringComparison.Ordinal))
            {
                _shimmerWord = _baseDescription[..spaceIndex];
                _restEscaped = _baseDescription[spaceIndex..].EscapeMarkup();
                _shimmerTimer = new Timer(OnShimmerTick, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(80));
            }
        }

        private void OnShimmerTick(object? state)
        {
            if (_shimmerStopped)
            {
                return;
            }

            try
            {
                int tick = Interlocked.Increment(ref _shimmerTick);
                int wordLen = _shimmerWord!.Length;
                // Wave sweeps across the word then briefly exits before re-entering.
                int totalPositions = wordLen + 6;
                int center = (tick % totalPositions) - 3;

                var sb = new StringBuilder();
                for (int i = 0; i < wordLen; i++)
                {
                    int distance = Math.Abs(i - center);
                    string ch = _shimmerWord[i].ToString().EscapeMarkup();

                    sb.Append(distance switch
                    {
                        0 => $"[white bold]{ch}[/]",
                        1 => $"[grey85]{ch}[/]",
                        _ => $"[grey]{ch}[/]",
                    });
                }

                sb.Append(_restEscaped);
                _task.Description = sb.ToString();
            }
            catch
            {
                // Shimmer is cosmetic — swallow any rendering errors silently.
            }
        }

        private void StopShimmer()
        {
            _shimmerStopped = true;
            _shimmerTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _shimmerTimer?.Dispose();
            _task.Description = _baseDescription;
        }

        public double Value
        {
            get => _task.Value;
            set
            {
                _task.Value = value;
                if (value >= _task.MaxValue && _shimmerTimer is not null && !_shimmerStopped)
                {
                    StopShimmer();
                }
            }
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
