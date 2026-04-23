// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
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

        RecordOptionUsage();

        try
        {
            _exitCode = ExecuteCore();
            return _exitCode;
        }
        catch (DotnetInstallException ex)
        {
            DotnetupTelemetry.Instance.RecordException(_operation, ex);
            AnsiConsole.MarkupLine(DotnetupTheme.Error($"Error: {ex.Message.EscapeMarkup()}"));
            return 1;
        }
        catch (Exception ex)
        {
            DotnetupTelemetry.Instance.RecordException(_operation, ex);
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

    internal void SetCommandTag(string key, object? value)
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

    /// <summary>
    /// Records which command-line options were explicitly provided and their PII-safe values.
    /// Iterates all options on the matched command (and parent commands) automatically.
    /// <list type="bullet">
    ///   <item>Enum options: emit the value if it is a defined member (e.g., "detailed")</item>
    ///   <item>Bool options: emit "true" / "false"</item>
    ///   <item>String/path options: emit only "redacted" (presence), never the raw value</item>
    /// </list>
    /// New options added to any command automatically get telemetry without changes here.
    /// </summary>
    private void RecordOptionUsage()
    {
        if (_operation is null)
        {
            return;
        }

        foreach (var option in ParseResult.CommandResult.Command.Options)
        {
            // Skip built-in options that don't carry useful signal
            if (option is HelpOption or VersionOption)
            {
                continue;
            }

            var result = ParseResult.GetResult(option);
            if (result is null)
            {
                continue;
            }

            // For bool flags, mere presence means explicit (ZeroOrOne arity, no token needed).
            // For value options, require at least one token so defaults aren't recorded.
            bool isBoolType = option.ValueType == typeof(bool) || option.ValueType == typeof(bool?);
            if (!isBoolType && result.Tokens.Count == 0)
            {
                continue;
            }

            string tagName = $"option.{OptionNameToTagKey(option.Name)}";
            string? tagValue = GetPiiSafeOptionValue(option, result);

            if (tagValue is not null)
            {
                _operation.Tag(tagName, tagValue);
            }
        }
    }

    /// <summary>
    /// Returns a PII-safe string representation of the option value, or null to suppress.
    /// </summary>
    private string? GetPiiSafeOptionValue(Option option, OptionResult result)
    {
        var valueType = option.ValueType;

        // Bool: always safe
        if (valueType == typeof(bool))
        {
            return ParseResult.GetValue((Option<bool>)option).ToString().ToLowerInvariant();
        }

        if (valueType == typeof(bool?))
        {
            var value = ParseResult.GetValue((Option<bool?>)option);
            return value?.ToString()?.ToLowerInvariant() ?? "true";
        }

        // Enum: emit the name if it's a defined member, "unknown" otherwise
        if (valueType.IsEnum)
        {
            var rawValue = result.GetValueForOption(option);
            if (rawValue is not null && Enum.IsDefined(valueType, rawValue))
            {
                return rawValue.ToString()!.ToLowerInvariant();
            }
            return "unknown";
        }

        // Numeric types: safe to emit directly
        if (valueType == typeof(int) || valueType == typeof(long) || valueType == typeof(double))
        {
            var rawValue = result.GetValueForOption(option);
            return rawValue?.ToString() ?? "unknown";
        }

        // Everything else (strings, paths, complex types): record presence only
        return "redacted";
    }

    /// <summary>
    /// Converts an option name like "--install-path" to a tag key like "install_path".
    /// </summary>
    private static string OptionNameToTagKey(string optionName)
    {
        return optionName.TrimStart('-').Replace('-', '_');
    }
}
