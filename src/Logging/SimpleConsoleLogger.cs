// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
        private readonly LogLevel _logLevel;

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

        public SimpleConsoleLogger(IConsole console, LogLevel logLevel)
        {
            _terminal = console.GetTerminal();
            _console = console;
            _logLevel = logLevel;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            lock (_gate)
            {
                var message = formatter(state, exception);
                if (_terminal is null)
                {
                    LogToConsole(message);
                }
                else
                {
                    LogToTerminal(message, logLevel);
                }
            }
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return (int)logLevel >= (int)_logLevel;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return NullScope.Instance;
        }

        void LogToTerminal(string message, LogLevel logLevel)
        {
            var messageColor = LogLevelColorMap[logLevel];
            _terminal.ForegroundColor = messageColor;
            _terminal.Out.Write($"  {message}{Environment.NewLine}");
            _terminal.ResetColor();
        }

        void LogToConsole(string message)
        {
            _console.Out.Write($"  {message}{Environment.NewLine}");
        }
    }
}
