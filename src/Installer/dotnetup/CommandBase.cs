// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using System.Diagnostics;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Base class for all dotnetup commands. Wires per-command telemetry: starts
/// a <see cref="TrackedOperation"/>, records exceptions, and disposes the op
/// (which emits the completion LogRecord) on exit.
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
            // Default success exit code; ExecuteCore can overwrite via
            // SetExitCode() (e.g. DotnetCommand forwarding a child process
            // exit code) or throw to signal failure (which leaves _exitCode
            // at its outer initialization of 1).
            _exitCode = 0;
            ExecuteCore();
            return _exitCode;
        }
        catch (Exception ex)
        {
            _exitCode = 1;
            DotnetupTelemetry.Instance.RecordException(_operation, ex);
            AnsiConsole.MarkupLine(DotnetupTheme.Error($"Error: {ex.Message.EscapeMarkup()}"));
#if DEBUG
            // Use ToString() (vs StackTrace) so inner exceptions and the
            // exception type name are preserved for debugging.
            if (ex is not DotnetInstallException)
            {
                Console.Error.WriteLine(ex.ToString());
            }
#endif
            return _exitCode;
        }
        finally
        {
            // _exitCode = 0 on success (set in try), 1 on exception (reset
            // in catch), or whatever ExecuteCore passed to SetExitCode().
            _operation?.Tag(TelemetryTagNames.ExitCode, _exitCode);
            _operation?.SetStatus(_exitCode == 0 ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
            _operation?.Dispose();
        }
    }

    /// <summary>
    /// Implement the command's core logic. Throw
    /// <see cref="DotnetInstallException"/> on failure so
    /// <see cref="Execute"/> can stamp <c>error.*</c> tags via
    /// <see cref="DotnetupTelemetry.RecordException"/>; do NOT return
    /// without throwing for failures.
    /// </summary>
    /// <remarks>
    /// Successful completion implies exit code 0. Commands that need to
    /// surface a different non-error exit code (e.g. forwarding a child
    /// process's exit code via <c>dotnetup dotnet ...</c>) must call
    /// <see cref="SetExitCode"/> before returning.
    /// </remarks>
    protected abstract void ExecuteCore();

    /// <summary>
    /// Overrides the success exit code that <see cref="Execute"/> would
    /// otherwise return as 0. Intended for the narrow case of forwarding a
    /// child process's exit code (e.g. <c>dotnetup dotnet build</c>); do NOT
    /// use this as a substitute for throwing on failures (that bypasses the
    /// telemetry pipeline).
    /// </summary>
    protected void SetExitCode(int exitCode)
    {
        _exitCode = exitCode;
    }

    /// <summary>
    /// Returns the stable command identifier used as the
    /// <c>command.name</c> telemetry tag (e.g., <c>"sdk/install"</c>,
    /// <c>"runtime/update"</c>). Used only for telemetry; not surfaced to
    /// users.
    /// </summary>
    protected abstract string GetCommandName();

    /// <summary>
    /// Adds a tag to the per-command <see cref="TrackedOperation"/>. The
    /// tag is folded into the completion LogRecord state on dispose.
    /// </summary>
    internal void SetCommandTag(string key, object? value)
    {
        _operation?.Tag(key, value);
    }

    /// <summary>
    /// Tags the per-command operation with the version or channel the user
    /// requested (e.g., <c>"9.0"</c>, <c>"LTS"</c>, <c>"latest"</c>),
    /// sanitized via <see cref="VersionSanitizer"/> so arbitrary user input
    /// can't leak into the <c>dotnet.requested_version</c> telemetry tag.
    /// </summary>
    protected void RecordRequestedVersion(string? versionOrChannel)
    {
        var sanitized = VersionSanitizer.Sanitize(versionOrChannel);
        _operation?.Tag(TelemetryTagNames.DotnetRequestedVersion, sanitized);
    }

    /// <summary>
    /// Tags the per-command operation with where the requested version came
    /// from (e.g., <c>"cli"</c>, <c>"global.json"</c>) and, when available,
    /// the sanitized requested value itself. Lets dashboards distinguish
    /// explicit user requests from inferred / file-sourced ones.
    /// </summary>
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
            // Distinguish "--flag" (true), "--flag false" (false), and
            // "option not provided" (null). Returning "true" for null was a
            // bug — it conflated absent with present.
            return value?.ToString()?.ToLowerInvariant() ?? "null";
        }

        // Enum: emit the name if it's a defined member, "unknown" otherwise
        if (valueType.IsEnum)
        {
            var rawValue = result.GetValueOrDefault<object>();
            if (rawValue is not null && Enum.IsDefined(valueType, rawValue))
            {
                return rawValue.ToString()!.ToLowerInvariant();
            }
            return "unknown";
        }

        // Numeric types: safe to emit directly
        if (valueType == typeof(int) || valueType == typeof(long) || valueType == typeof(double))
        {
            var rawValue = result.GetValueOrDefault<object>();
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
