// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.NET.StringTools;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Microsoft.NET.Build.Containers.Logging;

/// <summary>
/// Implements an ILogger that passes the logs to the wrapped TaskLoggingHelper.
/// </summary>
internal sealed class MSBuildLogger : ILogger
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

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = FormatMessage(_category, state, exception, formatter, _scopeProvider);
        switch (logLevel)
        {
            case LogLevel.Trace:
                _loggingHelper.LogMessage(MessageImportance.Low, message);
                break;
            case LogLevel.Debug:
            case LogLevel.Information:
                _loggingHelper.LogMessage(MessageImportance.High, message);
                break;
            case LogLevel.Warning:
                _loggingHelper.LogWarning(message);
                break;
            case LogLevel.Error:
            case LogLevel.Critical:
                _loggingHelper.LogError(message);
                break;
            case LogLevel.None:
                break;
            default:
                break;
        }
    }

    public static string FormatMessage<TState>(string category, TState state, Exception? exception, Func<TState, Exception?, string> formatter, IExternalScopeProvider? scopeProvider)
    {
        using var builder = new SpanBasedStringBuilder();
        var categoryBlock = string.Concat("[".AsSpan(), category.AsSpan(), "] ".AsSpan());
        builder.Append(categoryBlock);
        var formatted = formatter(state, exception);
        builder.Append(formatted);

        if (scopeProvider is not null)
        {
            // state will be a FormattedLogValues instance
            // scope will be our dictionary thing we need to probe into
            scopeProvider.ForEachScope((scope, state) =>
            {
                var stateItems = state as IReadOnlyList<KeyValuePair<string, object?>>;
                var originalFormat = (stateItems?.FirstOrDefault(kvp => kvp.Key == "{OriginalFormat}").Value as string)!;

                if (scope is IDictionary<string, object> dict)
                {
                    foreach (var kvp in dict)
                    {
                        if (kvp.Key == "{OriginalFormat}")
                        {
                            // Skip the original format key
                            continue;
                        }

                        var wrappedKey = "{" + kvp.Key + "}";
                        if (originalFormat.Contains(wrappedKey))
                        {
                            // If the key is part of the format string of the original format, we don't need to append it again.
                            continue;
                        }

                        builder.Append($" {kvp.Key}={kvp.Value}");
                    }
                }
                else if (scope is string s)
                {
                    builder.Append($" {s}");
                }
            }, state);
        }

        return builder.ToString();
    }

    /// <summary>
    /// A simple disposable to describe scopes with <see cref="BeginScope{TState}(TState)"/>.
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
