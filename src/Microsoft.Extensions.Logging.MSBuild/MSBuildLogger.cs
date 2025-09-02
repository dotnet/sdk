// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.StringTools;

namespace Microsoft.Extensions.Logging.MSBuild;

/// <summary>
/// Implements an ILogger that passes the logs to the wrapped TaskLoggingHelper.
/// </summary>
/// <remarks>
/// This logger is designed to be used with MSBuild tasks, allowing logs to be written in a way that integrates with the MSBuild logging system.
/// It looks for specific property names in the state/scope parts of the message and maps them to the parameters of the MSBuild LogX methods.
/// Those specific keys are:
/// <list type="bullet">
/// <item><term>Subcategory</term></item>
/// <item><term>Code</term></item>
/// <item><term>HelpKeyword</term></item>
/// <item><term>File</term></item>
/// <item><term>LineNumber</term></item>
/// <item><term>ColumnNumber</term></item>
/// <item><term>EndLineNumber</term></item>
/// <item><term>EndColumnNumber</term></item>
/// <item><term>{OriginalFormat}</term><description>(usually provided by the underlying logging framework)</description></item>
/// </list>
///
/// So if you add these to the scope (e.g. via <code lang="csharp">_logger.BeginScope(new Dictionary&lt;string, object&gt;{ ... }))</code> or on the message format itself,
/// they will be extracted and used to format the message correctly for MSBuild.
/// </remarks>
public sealed class MSBuildLogger : ILogger
{
    private static readonly IDisposable Scope = new DummyDisposable();

    private readonly TaskLoggingHelper _loggingHelper;
    private readonly string _category;
    private IExternalScopeProvider? _scopeProvider;

    public MSBuildLogger(string category, TaskLoggingHelper loggingHelperToWrap, IExternalScopeProvider? scopeProvider = null)
    {
        _category = category;
        _loggingHelper = loggingHelperToWrap;
        _scopeProvider = scopeProvider;
    }

    IDisposable ILogger.BeginScope<TState>(TState state) => _scopeProvider?.Push(state) ?? Scope;

    public bool IsEnabled(LogLevel logLevel) =>
        logLevel switch
        {
            LogLevel.Trace => _loggingHelper.LogsMessagesOfImportance(MessageImportance.Low),
            LogLevel.Debug => _loggingHelper.LogsMessagesOfImportance(MessageImportance.Normal),
            LogLevel.Information => _loggingHelper.LogsMessagesOfImportance(MessageImportance.High),
            LogLevel.Warning or LogLevel.Error or LogLevel.Critical => true,
            LogLevel.None => false,
            _ => throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null)
        };

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = FormatMessage(_category, state, exception, formatter, _scopeProvider);
        switch (logLevel)
        {
            case LogLevel.Trace:
                _loggingHelper.LogMessage(message.subcategory, message.code, message.helpKeyword, message.file, message.lineNumber ?? 0, message.columnNumber ?? 0, message.endLineNumber ?? 0, message.endColumnNumber ?? 0, MessageImportance.Low, message.message);
                break;
            case LogLevel.Debug:
                _loggingHelper.LogMessage(message.subcategory, message.code, message.helpKeyword, message.file, message.lineNumber ?? 0, message.columnNumber ?? 0, message.endLineNumber ?? 0, message.endColumnNumber ?? 0, MessageImportance.Normal, message.message);
                break;
            case LogLevel.Information:
                _loggingHelper.LogMessage(message.subcategory, message.code, message.helpKeyword, message.file, message.lineNumber ?? 0, message.columnNumber ?? 0, message.endLineNumber ?? 0, message.endColumnNumber ?? 0, MessageImportance.High, message.message);
                break;
            case LogLevel.Warning:
                _loggingHelper.LogWarning(message.subcategory, message.code, message.helpKeyword, message.helpLink, message.file, message.lineNumber ?? 0, message.columnNumber ?? 0, message.endLineNumber ?? 0, message.endColumnNumber ?? 0, message.message);
                break;
            case LogLevel.Error:
            case LogLevel.Critical:
                _loggingHelper.LogError(message.subcategory, message.code, message.helpKeyword, message.helpLink, message.file, message.lineNumber ?? 0, message.columnNumber ?? 0, message.endLineNumber ?? 0, message.endColumnNumber ?? 0, message.message);
                break;
            case LogLevel.None:
                break;
            default:
                break;
        }
    }

    private static MSBuildMessageParameters FormatMessage<TState>(string category, TState state, Exception? exception, Func<TState, Exception?, string> formatter, IExternalScopeProvider? scopeProvider)
    {
        MSBuildMessageParameters message = default;
        using var builder = new SpanBasedStringBuilder();
        var categoryBlock = string.Concat("[".AsSpan(), category.AsSpan(), "] ".AsSpan());
        builder.Append(categoryBlock);
        var formatted = formatter(state, exception);
        builder.Append(formatted);

        // any unprocessed state items will be appended to the message after scope processing 
        var unprocessedKeyValues = ProcessState(state, ref message, out string? originalFormat);

        // scope will be our dictionary thing we need to probe into
        scopeProvider?.ForEachScope((scope, state) => ProcessScope(scope, ref message, ref originalFormat, unprocessedKeyValues), state);

        Debug.Assert(originalFormat is not null, "Original format should not be null at this point - either state or scope should have provided it.");

        ApplyUnprocessedItemsToMessage(unprocessedKeyValues, originalFormat, builder);

        message.message = builder.ToString();
        return message;
    }

    private static void ProcessScope(object? scope, ref MSBuildMessageParameters message, ref string? originalFormat, List<KeyValuePair<string, object?>>? unprocessedKeyValues)
    {
        if (scope is IDictionary<string, object?> dict)
        {
            foreach (var kvp in dict)
            {
                switch (kvp.Key)
                {
                    case "{OriginalFormat}":
                        if (originalFormat is null && kvp.Value is string format)
                        {
                            originalFormat = format;
                        }
                        continue;
                    case "Subcategory":
                        message.subcategory = kvp.Value as string;
                        continue;
                    case "Code":
                        message.code = kvp.Value as string;
                        continue;
                    case "HelpKeyword":
                        message.helpKeyword = kvp.Value as string;
                        continue;
                    case "HelpLink":
                        message.helpLink = kvp.Value as string;
                        continue;
                    case "File":
                        message.file = kvp.Value as string;
                        continue;
                    case "LineNumber":
                        if (kvp.Value is int lineNumber)
                            message.lineNumber = lineNumber;
                        continue;
                    case "ColumnNumber":
                        if (kvp.Value is int columnNumber)
                            message.columnNumber = columnNumber;
                        continue;
                    case "EndLineNumber":
                        if (kvp.Value is int endLineNumber)
                            message.endLineNumber = endLineNumber;
                        continue;
                    case "EndColumnNumber":
                        if (kvp.Value is int endColumnNumber)
                            message.endColumnNumber = endColumnNumber;
                        continue;
                    default:
                        unprocessedKeyValues ??= [];
                        unprocessedKeyValues.Add(kvp);
                        continue;
                }
            }
        }
        else if (scope is string s)
        {
            unprocessedKeyValues ??= [];
            // If the scope is a string, we treat it as an unprocessed item
            unprocessedKeyValues.Add(new KeyValuePair<string, object?>("Scope", s));
        }
    }

    private static void ApplyUnprocessedItemsToMessage(List<KeyValuePair<string, object?>>? unprocessedStateItems, string originalFormat, SpanBasedStringBuilder builder)
    {
        // foreach unprocessed item, if the format string does not contain the key, append it to the message
        // in key=value format using the builder
        if (unprocessedStateItems is not null)
        {
            foreach (var kvp in unprocessedStateItems)
            {
                var wrappedKey = "{" + kvp.Key + "}";
                if (!originalFormat.Contains(wrappedKey))
                {
                    builder.Append($" {kvp.Key}={kvp.Value}");
                }
            }
        }
    }

    private static List<KeyValuePair<string, object?>>? ProcessState<TState>(TState state, ref MSBuildMessageParameters message, out string? originalFormat)
    {
        originalFormat = null;
        List<KeyValuePair<string, object?>>? unmappedStateItems = null;
        if (state is IReadOnlyList<KeyValuePair<string, object?>> stateItems)
        {
            foreach (var kvp in stateItems)
            {
                switch (kvp.Key)
                {
                    case "{OriginalFormat}":
                        // If the key is {OriginalFormat}, we will use it to set the originalFormat variable.
                        // This is used to avoid appending the same key again in the message.
                        if (kvp.Value is string format)
                        {
                            originalFormat = format;
                        }
                        continue;
                    case "Subcategory":
                        message.subcategory = kvp.Value as string;
                        continue;
                    case "Code":
                        message.code = kvp.Value as string;
                        continue;
                    case "HelpKeyword":
                        message.helpKeyword = kvp.Value as string;
                        continue;
                    case "HelpLink":
                        message.helpLink = kvp.Value as string;
                        continue;
                    case "File":
                        message.file = kvp.Value as string;
                        continue;
                    case "LineNumber":
                        if (kvp.Value is int lineNumber)
                            message.lineNumber = lineNumber;
                        continue;
                    case "ColumnNumber":
                        if (kvp.Value is int columnNumber)
                            message.columnNumber = columnNumber;
                        continue;
                    case "EndLineNumber":
                        if (kvp.Value is int endLineNumber)
                            message.endLineNumber = endLineNumber;
                        continue;
                    case "EndColumnNumber":
                        if (kvp.Value is int endColumnNumber)
                            message.endColumnNumber = endColumnNumber;
                        continue;
                    default:
                        unmappedStateItems ??= [];
                        unmappedStateItems.Add(kvp);
                        continue;
                }
            }
            return unmappedStateItems;
        }
        else
        {
            // If the state is not a list, we just create an empty message.
            message = new MSBuildMessageParameters();
        }
        return null;
    }


    /// <summary>
    /// A struct that maps to the parameters of the MSBuild LogX methods. We'll extract this from M.E.ILogger state/scope information so that we can be maximally compatible with the MSBuild logging system.
    /// </summary>
    private record struct MSBuildMessageParameters(string? subcategory,
            string? code,
            string? helpKeyword,
            string? helpLink,
            string? file,
            int? lineNumber,
            int? columnNumber,
            int? endLineNumber,
            int? endColumnNumber,
            string message);

    /// <summary>
    /// A simple disposable to describe scopes with <see cref="ILogger.BeginScope"/>.
    /// </summary>
    private sealed class DummyDisposable : IDisposable
    {
        public void Dispose() { }
    }

    internal void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }
}
