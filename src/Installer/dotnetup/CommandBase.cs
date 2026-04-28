// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Diagnostics;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Base class for all dotnetup commands with automatic telemetry.
/// Uses the template method pattern to wrap command execution with telemetry.
/// </summary>
public abstract class CommandBase
{
    protected ParseResult ParseResult { get; }
    private Activity? _commandActivity;
    private int _exitCode;

    protected CommandBase(ParseResult parseResult)
    {
        ParseResult = parseResult;
    }

    /// <summary>
    /// Executes the command with automatic telemetry tracking.
    /// Activities automatically track duration via start/stop — no Stopwatch needed.
    /// </summary>
    /// <returns>The exit code of the command.</returns>
    public int Execute()
    {
        var commandName = GetCommandName();
        _commandActivity = DotnetupTelemetry.Instance.StartCommand(commandName);
        _exitCode = 1;

        try
        {
            _exitCode = ExecuteCore();
            return _exitCode;
        }
        catch (DotnetInstallException ex)
        {
            // Known installation errors - print a clean user-friendly message
            DotnetupTelemetry.Instance.RecordException(_commandActivity, ex);
            AnsiConsole.MarkupLine(DotnetupTheme.Error($"Error: {ex.Message.EscapeMarkup()}"));
            return 1;
        }
        catch (Exception ex)
        {
            // Unexpected errors - still record telemetry so error_type is populated
            DotnetupTelemetry.Instance.RecordException(_commandActivity, ex);
            AnsiConsole.MarkupLine(DotnetupTheme.Error($"Error: {ex.Message.EscapeMarkup()}"));
#if DEBUG
            Console.Error.WriteLine(ex.StackTrace);
#endif
            return 1;
        }
        finally
        {
            _commandActivity?.SetTag(TelemetryTagNames.ExitCode, _exitCode);
            _commandActivity?.SetStatus(_exitCode == 0 ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
            _commandActivity?.Dispose();
        }
    }

    /// <summary>
    /// Implement this method to provide the command's core logic.
    /// </summary>
    /// <returns>The exit code of the command.</returns>
    protected abstract int ExecuteCore();

    /// <summary>
    /// Gets the command name for telemetry purposes (e.g., "sdk/install", "list").
    /// </summary>
    protected abstract string GetCommandName();

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
    /// Records the requested version/channel with PII sanitization.
    /// Only known safe patterns are passed through; unknown patterns are replaced with "invalid".
    /// </summary>
    /// <param name="versionOrChannel">The raw version or channel string from user input.</param>
    protected void RecordRequestedVersion(string? versionOrChannel)
    {
        var sanitized = VersionSanitizer.Sanitize(versionOrChannel);
        _commandActivity?.SetTag(TelemetryTagNames.DotnetRequestedVersion, sanitized);
    }

    /// <summary>
    /// Records the source of the install request (explicit user input vs default).
    /// </summary>
    /// <param name="source">The request source: "explicit", "default-latest", or "default-globaljson".</param>
    /// <param name="requestedValue">The sanitized requested value (channel/version). For defaults, this is what was defaulted to.</param>
    protected void RecordRequestSource(string source, string? requestedValue)
    {
        _commandActivity?.SetTag(TelemetryTagNames.DotnetRequestSource, source);
        if (requestedValue != null)
        {
            var sanitized = VersionSanitizer.Sanitize(requestedValue);
            _commandActivity?.SetTag(TelemetryTagNames.DotnetRequested, sanitized);
        }
    }
}
