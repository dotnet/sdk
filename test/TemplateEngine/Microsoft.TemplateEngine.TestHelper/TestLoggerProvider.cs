// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.Extensions.Logging;

namespace Microsoft.TemplateEngine.TestHelper
{
    /// <summary>
    /// Microsoft.Extensions.Logging <see cref="ILoggerProvider"/> which routes log
    /// messages to a write callback (typically the test runner's diagnostic output).
    /// </summary>
    /// <remarks>
    /// See https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Logging/tests/DI.Common/Common/src/XunitLoggerProvider.cs for more details.
    /// </remarks>
    public class TestLoggerProvider : ILoggerProvider
    {
        private readonly Action<string> _writeLine;
        private readonly LogLevel _minLevel;
        private readonly DateTimeOffset? _logStart;

        public TestLoggerProvider(Action<string> writeLine)
            : this(writeLine, LogLevel.Trace)
        {
        }

        public TestLoggerProvider(Action<string> writeLine, LogLevel minLevel)
            : this(writeLine, minLevel, null)
        {
        }

        public TestLoggerProvider(Action<string> writeLine, LogLevel minLevel, DateTimeOffset? logStart)
        {
            _writeLine = writeLine;
            _minLevel = minLevel;
            _logStart = logStart;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new TestLogger(_writeLine, categoryName, _minLevel, _logStart);
        }

        public void Dispose()
        {
        }

        private class TestLogger : ILogger
        {
            private static readonly string[] NewLineChars = new[] { Environment.NewLine };
            private readonly string _category;
            private readonly LogLevel _minLogLevel;
            private readonly Action<string> _writeLine;
            private readonly DateTimeOffset? _logStart;

            public TestLogger(Action<string> writeLine, string category, LogLevel minLogLevel, DateTimeOffset? logStart)
            {
                _minLogLevel = minLogLevel;
                _category = category;
                _writeLine = writeLine;
                _logStart = logStart;
            }

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel))
                {
                    return;
                }

                // Buffer the message into a single string in order to avoid shearing the message when running across multiple threads.
                var messageBuilder = new StringBuilder();

                var timestamp = _logStart.HasValue ? $"{(DateTimeOffset.UtcNow - _logStart.Value).TotalSeconds:N3}s" : DateTimeOffset.UtcNow.ToString("s");

                var firstLinePrefix = $"| [{timestamp}] {_category} {logLevel}: ";
                var lines = formatter(state, exception).Split(NewLineChars, StringSplitOptions.RemoveEmptyEntries);
                messageBuilder.AppendLine(firstLinePrefix + (lines.FirstOrDefault() ?? string.Empty));

                var additionalLinePrefix = "|" + new string(' ', firstLinePrefix.Length - 1);
                foreach (var line in lines.Skip(1))
                {
                    messageBuilder.AppendLine(additionalLinePrefix + line);
                }

                if (exception != null)
                {
                    lines = exception.ToString().Split(NewLineChars, StringSplitOptions.RemoveEmptyEntries);
                    additionalLinePrefix = "| ";
                    foreach (var line in lines)
                    {
                        messageBuilder.AppendLine(additionalLinePrefix + line);
                    }
                }

                // Remove the last line-break, because the write callback only has a line-oriented API.
                var message = messageBuilder.ToString();
                if (message.EndsWith(Environment.NewLine))
                {
                    message = message.Substring(0, message.Length - Environment.NewLine.Length);
                }

                try
                {
                    _writeLine(message);
                }
                catch (Exception)
                {
                    // We could fail because we're on a background thread and our captured callback is
                    // busted (if the test "completed" before the background thread fired).
                    // So, ignore this. There isn't really anything we can do but hope the
                    // caller has additional loggers registered
                }
            }

            public bool IsEnabled(LogLevel logLevel)
                => logLevel >= _minLogLevel;

            public IDisposable BeginScope<TState>(TState state) where TState : notnull
                => new NullScope();

            private class NullScope : IDisposable
            {
                public void Dispose()
                {
                }
            }
        }
    }
}
