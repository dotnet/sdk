// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Sdk.Publish.Tasks.OneDeploy;

/// <summary>
/// A simple logger definition for a <see cref="Task"/>.
/// </summary>
internal interface ITaskLogger
{
    /// <summary>
    /// Whether this logger is enabled.
    /// </summary>
    bool Enabled { get; set; }

    /// <summary>
    /// Logs a message.
    /// </summary>
    /// <param name="message">message to log</param>
    void LogMessage(string message);

    /// <summary>
    /// Logs a message.
    /// </summary>
    /// <param name="importance">message importance</param>
    /// <param name="message">message to log</param>
    void LogMessage(MessageImportance importance, string message);

    /// <summary>
    /// Logs an error message.
    /// </summary>
    /// <param name="message">message to log</param>
    void LogError(string message);

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    /// <param name="message">message to log</param>
    void LogWarning(string message);
}
