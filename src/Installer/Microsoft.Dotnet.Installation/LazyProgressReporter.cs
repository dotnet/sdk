// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation;

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
            return _inner.AddTask(description, maxValue);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _inner?.Dispose();
        }
    }
}
