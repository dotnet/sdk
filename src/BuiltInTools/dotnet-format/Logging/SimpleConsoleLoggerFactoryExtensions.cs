// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.CommandLine;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Logging
{
    internal static class SimpleConsoleLoggerFactoryExtensions
    {
        public static SimpleConsoleLoggerProvider Provider = null!;
        public static ILoggerFactory AddSimpleConsole(this ILoggerFactory factory, IConsole console, LogLevel minimalLogLevel, LogLevel minimalErrorLevel)
        {
            Provider = new SimpleConsoleLoggerProvider(console, minimalLogLevel, minimalErrorLevel);
            factory.AddProvider(Provider);
            return factory;
        }
    }
}
