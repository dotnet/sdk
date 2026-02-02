// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.Text;
using Microsoft.Dotnet.Installation;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;
using Spectre.Console;

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
            _commandActivity?.SetTag("duration_ms", stopwatch.Elapsed.TotalMilliseconds);
            _commandActivity?.SetStatus(exitCode == 0 ? ActivityStatusCode.Ok : ActivityStatusCode.Error);

            return exitCode;
        }
        catch (DotnetInstallException ex)
        {
            // Known installation errors - print a clean user-friendly message
            stopwatch.Stop();
            _commandActivity?.SetTag("duration_ms", stopwatch.Elapsed.TotalMilliseconds);
            _commandActivity?.SetTag("exit.code", 1);
            DotnetupTelemetry.Instance.RecordException(_commandActivity, ex);
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message.EscapeMarkup()}[/]");
            return 1;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _commandActivity?.SetTag("duration_ms", stopwatch.Elapsed.TotalMilliseconds);
            _commandActivity?.SetTag("exit.code", 1);
            DotnetupTelemetry.Instance.RecordException(_commandActivity, ex);
            // Status is already set inside RecordException with error type (no PII)
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

    /// <summary>
    /// Records a failure reason without throwing an exception.
    /// Use this when returning a non-zero exit code to capture the error context.
    /// </summary>
    /// <param name="reason">A short error reason code (e.g., "path_mismatch", "download_failed").</param>
    /// <param name="message">Optional detailed error message.</param>
    /// <param name="category">Error category: "user" for input/environment issues, "product" for bugs (default).</param>
    protected void RecordFailure(string reason, string? message = null, string category = "product")
    {
        _commandActivity?.SetTag("error.type", reason);
        _commandActivity?.SetTag("error.category", category);
        if (message != null)
        {
            _commandActivity?.SetTag("error.message", message);
        }
    }

    /// <summary>
    /// Records the requested version/channel with PII sanitization.
    /// Only known safe patterns are passed through; unknown patterns are replaced with "invalid".
    /// </summary>
    /// <param name="versionOrChannel">The raw version or channel string from user input.</param>
    protected void RecordRequestedVersion(string? versionOrChannel)
    {
        var sanitized = VersionSanitizer.Sanitize(versionOrChannel);
        _commandActivity?.SetTag("sdk.requested_version", sanitized);
    }

    /// <summary>
    /// Records the source of the SDK request (explicit user input vs default).
    /// </summary>
    /// <param name="source">The request source: "explicit", "default-latest", or "default-globaljson".</param>
    /// <param name="requestedValue">The sanitized requested value (channel/version). For defaults, this is what was defaulted to.</param>
    protected void RecordRequestSource(string source, string? requestedValue)
    {
        _commandActivity?.SetTag("sdk.request_source", source);
        if (requestedValue != null)
        {
            var sanitized = VersionSanitizer.Sanitize(requestedValue);
            _commandActivity?.SetTag("sdk.requested", sanitized);
        }
    }
}
