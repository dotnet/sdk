// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.CommandLine;
using System.CommandLine.Rendering;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Logging
{
    internal class SimpleConsoleLogger : ILogger
    {
        private readonly object _gate = new object();

        private readonly IConsole _console;
        private readonly ITerminal _terminal;
        private readonly LogLevel _minimalLogLevel;
        private readonly LogLevel _minimalErrorLevel;

        private static ImmutableDictionary<LogLevel, ConsoleColor> LogLevelColorMap => new Dictionary<LogLevel, ConsoleColor>
        {
            [LogLevel.Critical] = ConsoleColor.Red,
            [LogLevel.Error] = ConsoleColor.Red,
            [LogLevel.Warning] = ConsoleColor.Yellow,
            [LogLevel.Information] = ConsoleColor.White,
            [LogLevel.Debug] = ConsoleColor.Gray,
            [LogLevel.Trace] = ConsoleColor.Gray,
            [LogLevel.None] = ConsoleColor.White,
        }.ToImmutableDictionary();

        public SimpleConsoleLogger(IConsole console, LogLevel minimalLogLevel, LogLevel minimalErrorLevel)
        {
            _terminal = console.GetTerminal();
            _console = console;
            _minimalLogLevel = minimalLogLevel;
            _minimalErrorLevel = minimalErrorLevel;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            lock (_gate)
            {
                var message = formatter(state, exception);
                var logToErrorStream = logLevel >= _minimalErrorLevel;
                if (_terminal is null)
                {
                    LogToConsole(_console, message, logToErrorStream);
                }
                else
                {
                    LogToTerminal(message, logLevel, logToErrorStream);
                }
            }
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return (int)logLevel >= (int)_minimalLogLevel;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return NullScope.Instance;
        }

        private void LogToTerminal(string message, LogLevel logLevel, bool logToErrorStream)
        {
            var messageColor = LogLevelColorMap[logLevel];
            _terminal.ForegroundColor = messageColor;

            LogToConsole(_terminal, message, logToErrorStream);

            _terminal.ResetColor();
        }

        private static void LogToConsole(IConsole console, string message, bool logToErrorStream)
        {
            if (logToErrorStream)
            {
                console.Error.Write($"{message}{Environment.NewLine}");
            }
            else
            {
                console.Out.Write($"  {message}{Environment.NewLine}");
            }
        }
    }
}
