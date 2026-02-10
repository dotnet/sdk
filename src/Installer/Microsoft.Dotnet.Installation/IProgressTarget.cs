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

    /// <summary>
    /// Adds a task with telemetry activity tracking.
    /// </summary>
    /// <param name="activityName">The name for the telemetry activity (e.g., "download", "extract").</param>
    /// <param name="description">The user-visible description.</param>
    /// <param name="maxValue">The maximum progress value.</param>
    IProgressTask AddTask(string activityName, string description, double maxValue)
        => AddTask(description, maxValue); // Default: no telemetry
}

public interface IProgressTask
{
    string Description { get; set; }
    double Value { get; set; }
    double MaxValue { get; set; }

    /// <summary>
    /// Sets a telemetry tag on the underlying activity (if any).
    /// </summary>
    void SetTag(string key, object? value) { }

    /// <summary>
    /// Records an error on the underlying activity (if any).
    /// </summary>
    void RecordError(Exception ex) { }

    /// <summary>
    /// Marks the task as successfully completed.
    /// </summary>
    void Complete() { }
}

public class NullProgressTarget : IProgressTarget
{
    public IProgressReporter CreateProgressReporter() => new NullProgressReporter();

    private sealed class NullProgressReporter : IProgressReporter
    {
        public void Dispose() { }

        public IProgressTask AddTask(string description, double maxValue)
            => new NullProgressTask(description);

        public IProgressTask AddTask(string activityName, string description, double maxValue)
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
