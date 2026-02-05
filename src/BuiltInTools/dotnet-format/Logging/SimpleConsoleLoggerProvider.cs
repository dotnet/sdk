// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Logging
{
    internal class SimpleConsoleLoggerProvider : ILoggerProvider
    {
        private readonly LogLevel _minimalLogLevel;
        private readonly LogLevel _minimalErrorLevel;

        public SimpleConsoleLoggerProvider(LogLevel minimalLogLevel, LogLevel minimalErrorLevel)
        {
            _minimalLogLevel = minimalLogLevel;
            _minimalErrorLevel = minimalErrorLevel;
        }

        public ILogger CreateLogger(string name)
        {
            return new SimpleConsoleLogger(_minimalLogLevel, _minimalErrorLevel);
        }

        public void Dispose()
        {
        }
    }
}
