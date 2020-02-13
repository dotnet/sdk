using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Perf
{
    internal class EmptyLogger : ILogger
    {

        public void Log<TState>(LogLevel logLevel,
                                EventId eventId,
                                TState state,
                                Exception exception,
                                Func<TState, Exception, string> formatter)
        {
        }

        public bool IsEnabled(LogLevel logLevel) => false;

        public IDisposable BeginScope<TState>(TState state) => new EmptyScope();

        private class EmptyScope : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
