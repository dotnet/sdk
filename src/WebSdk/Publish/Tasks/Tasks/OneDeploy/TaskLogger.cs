// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Sdk.Publish.Tasks.OneDeploy;

/// <summary>
/// A logger that wraps a <see cref="TaskLoggingHelper"/> instance.
/// </summary>
internal class TaskLogger(TaskLoggingHelper logger, bool isEnabled = true) : ITaskLogger
{
    private readonly TaskLoggingHelper _taskLoggingHelper = logger;

    /// <inheritdoc/>
    public bool Enabled { get; set; } = isEnabled;

    private bool CanLog => _taskLoggingHelper is not null && Enabled;

    /// <inheritdoc/>
    public void LogMessage(string message)
    {
        if (CanLog)
        {
            _taskLoggingHelper.LogMessage(message);
        }
    }

    /// <inheritdoc/>
    public void LogMessage(MessageImportance importance, string message)
    {
        if (CanLog)
        {
            _taskLoggingHelper.LogMessage(importance, message);

        }
    }

    /// <inheritdoc/>
    public void LogError(string message)
    {
        if (CanLog)
        {
            _taskLoggingHelper.LogError(message);

        }
    }

    /// <inheritdoc/>
    public void LogWarning(string message)
    {
        if (CanLog)
        {
            _taskLoggingHelper.LogWarning(message);
        }
    }
}
