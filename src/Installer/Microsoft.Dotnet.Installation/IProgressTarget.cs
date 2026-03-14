// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation;

public interface IProgressTarget
{
    IProgressReporter CreateProgressReporter();
}

public interface IProgressReporter : IDisposable
{
    IProgressTask AddTask(string description, double maxValue);
}

public interface IProgressTask
{
    string Description { get; set; }
    double Value { get; set; }
    double MaxValue { get; set; }
}

public class NullProgressTarget : IProgressTarget
{
    public IProgressReporter CreateProgressReporter() => new NullProgressReporter();

    private sealed class NullProgressReporter : IProgressReporter
    {
        public void Dispose() { }

        public IProgressTask AddTask(string description, double maxValue)
            => new NullProgressTask(description);
    }

    private sealed class NullProgressTask : IProgressTask
    {
        public NullProgressTask(string description)
        {
            Description = description;
        }

        public double Value { get; set; }
        public string Description { get; set; }
        public double MaxValue { get; set; }
    }
}

/// <summary>
/// Defers creation of the underlying <see cref="IProgressReporter"/> until the first task is added.
/// This avoids Spectre.Console rendering an empty progress bar (and leaving a blank line)
/// when an error occurs before any progress is reported.
/// </summary>
public sealed class LazyProgressReporter : IProgressReporter
{
    private readonly IProgressTarget _target;
    private readonly object _lock = new();
    private IProgressReporter? _inner;

    public LazyProgressReporter(IProgressTarget target)
    {
        _target = target;
    }

    public IProgressTask AddTask(string description, double maxValue)
    {
        lock (_lock)
        {
            _inner ??= _target.CreateProgressReporter();
        }

        return _inner.AddTask(description, maxValue);
    }

    public void Dispose()
    {
        _inner?.Dispose();
    }
}
