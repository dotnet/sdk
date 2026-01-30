// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.Text;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Base class for all dotnetup commands with automatic telemetry.
/// Uses the template method pattern to wrap command execution with telemetry.
/// </summary>
public abstract class CommandBase
{
    protected ParseResult _parseResult;
    private Activity? _commandActivity;

    protected CommandBase(ParseResult parseResult)
    {
        _parseResult = parseResult;
    }

    /// <summary>
    /// Executes the command with automatic telemetry tracking.
    /// </summary>
    /// <returns>The exit code of the command.</returns>
    public int Execute()
    {
        var commandName = GetCommandName();
        _commandActivity = DotnetupTelemetry.Instance.StartCommand(commandName);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var exitCode = ExecuteCore();

            stopwatch.Stop();
            _commandActivity?.SetTag("exit.code", exitCode);
            _commandActivity?.SetStatus(exitCode == 0 ? ActivityStatusCode.Ok : ActivityStatusCode.Error);

            // Post completion event for metrics
            DotnetupTelemetry.Instance.PostEvent("command/completed", new Dictionary<string, string>
            {
                ["command"] = commandName,
                ["exit_code"] = exitCode.ToString(),
                ["success"] = (exitCode == 0).ToString()
            }, new Dictionary<string, double>
            {
                ["duration_ms"] = stopwatch.Elapsed.TotalMilliseconds
            });

            return exitCode;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            DotnetupTelemetry.Instance.RecordException(_commandActivity, ex);

            // Post failure event
            var errorInfo = ErrorCodeMapper.GetErrorInfo(ex);
            var props = new Dictionary<string, string>
            {
                ["command"] = commandName,
                ["error_type"] = errorInfo.ErrorType
            };
            if (errorInfo.StatusCode.HasValue)
            {
                props["http_status"] = errorInfo.StatusCode.Value.ToString();
            }
            if (errorInfo.HResult.HasValue)
            {
                props["hresult"] = errorInfo.HResult.Value.ToString();
            }
            DotnetupTelemetry.Instance.PostEvent("command/failed", props, new Dictionary<string, double>
            {
                ["duration_ms"] = stopwatch.Elapsed.TotalMilliseconds
            });

            throw;
        }
        finally
        {
            _commandActivity?.Dispose();
        }
    }

    /// <summary>
    /// Implement this method to provide the command's core logic.
    /// </summary>
    /// <returns>The exit code of the command.</returns>
    protected abstract int ExecuteCore();

    /// <summary>
    /// Gets the command name for telemetry purposes.
    /// Override to provide a custom name.
    /// </summary>
    /// <returns>The command name (e.g., "sdk/install").</returns>
    protected virtual string GetCommandName()
    {
        // Default: derive from class name (SdkInstallCommand -> "sdkinstall")
        var name = GetType().Name;
        return name.Replace("Command", string.Empty, StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
    }

    /// <summary>
    /// Adds a tag to the current command activity.
    /// </summary>
    /// <param name="key">The tag key.</param>
    /// <param name="value">The tag value.</param>
    protected void SetCommandTag(string key, object? value)
    {
        _commandActivity?.SetTag(key, value);
    }
}
