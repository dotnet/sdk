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
/// Uses TrackedOperation for automatic event emission on dispose.
/// </summary>
public abstract class CommandBase
{
    protected ParseResult ParseResult { get; }
    private TrackedOperation? _operation;
    private int _exitCode;

    protected CommandBase(ParseResult parseResult)
    {
        ParseResult = parseResult;
    }

    public int Execute()
    {
        var commandName = GetCommandName();
        _operation = DotnetupTelemetry.Instance.StartTrackedCommand(commandName);
        _exitCode = 1;

        try
        {
            _exitCode = ExecuteCore();
            return _exitCode;
        }
        catch (DotnetInstallException ex)
        {
            DotnetupTelemetry.Instance.RecordException(_operation?.Activity, ex);
            AnsiConsole.MarkupLine(DotnetupTheme.Error($"Error: {ex.Message.EscapeMarkup()}"));
            return 1;
        }
        catch (Exception ex)
        {
            DotnetupTelemetry.Instance.RecordException(_operation?.Activity, ex);
            AnsiConsole.MarkupLine(DotnetupTheme.Error($"Error: {ex.Message.EscapeMarkup()}"));
#if DEBUG
            Console.Error.WriteLine(ex.StackTrace);
#endif
            return 1;
        }
        finally
        {
            _operation?.Tag(TelemetryTagNames.ExitCode, _exitCode);
            _operation?.SetStatus(_exitCode == 0 ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
            _operation?.Dispose();
        }
    }

    protected abstract int ExecuteCore();

    protected abstract string GetCommandName();

    protected void SetCommandTag(string key, object? value)
    {
        _operation?.Tag(key, value);
    }

    protected void RecordRequestedVersion(string? versionOrChannel)
    {
        var sanitized = VersionSanitizer.Sanitize(versionOrChannel);
        _operation?.Tag(TelemetryTagNames.DotnetRequestedVersion, sanitized);
    }

    protected void RecordRequestSource(string source, string? requestedValue)
    {
        _operation?.Tag(TelemetryTagNames.DotnetRequestSource, source);
        if (requestedValue != null)
        {
            var sanitized = VersionSanitizer.Sanitize(requestedValue);
            _operation?.Tag(TelemetryTagNames.DotnetRequested, sanitized);
        }
    }
}
