// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Tools.Logging;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Tests.Utilities
{
    /// <summary>
    /// Logger that records all logged messages.
    /// </summary>
    internal class TestLogger : ILogger
    {
        private readonly StringBuilder _builder = new StringBuilder();

        public IDisposable BeginScope<TState>(TState state)
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var message = formatter(state, exception);
            _builder.AppendLine(message);
        }

        public string GetLog()
        {
            return _builder.ToString();
        }
    }
}
