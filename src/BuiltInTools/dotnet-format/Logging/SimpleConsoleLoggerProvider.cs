// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.CommandLine;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Logging
{
    internal class SimpleConsoleLoggerProvider : ILoggerProvider
    {
        private readonly IConsole _console;
        private readonly LogLevel _minimalLogLevel;
        private readonly LogLevel _minimalErrorLevel;

        public SimpleConsoleLoggerProvider(IConsole console, LogLevel minimalLogLevel, LogLevel minimalErrorLevel)
        {
            _console = console;
            _minimalLogLevel = minimalLogLevel;
            _minimalErrorLevel = minimalErrorLevel;
        }

        public ILogger CreateLogger(string name)
        {
            return new SimpleConsoleLogger(_console, _minimalLogLevel, _minimalErrorLevel);
        }

        public void Dispose()
        {
        }
    }
}
