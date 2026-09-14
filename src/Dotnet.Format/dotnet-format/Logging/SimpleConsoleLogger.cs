// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Logging
{
    internal class SimpleConsoleLogger : ILogger
    {
        private static readonly bool ColorsAreSupported =
            !(OperatingSystem.IsBrowser() || OperatingSystem.IsAndroid() || OperatingSystem.IsIOS() || OperatingSystem.IsTvOS())
                && !Console.IsOutputRedirected;

        private readonly static object _gate = new object();

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

        public SimpleConsoleLogger(LogLevel minimalLogLevel, LogLevel minimalErrorLevel)
        {
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

                Log(message, logLevel, logToErrorStream);
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

        private void Log(string message, LogLevel logLevel, bool logToErrorStream)
        {
            if (ColorsAreSupported)
            {
                Console.ForegroundColor = LogLevelColorMap[logLevel];
            }

            if (logToErrorStream)
            {
                Console.Error.WriteLine(message);
            }
            else
            {
                Console.Out.WriteLine(message);
            }

            if (ColorsAreSupported)
            {
                Console.ResetColor();
            }
        }
    }
}
