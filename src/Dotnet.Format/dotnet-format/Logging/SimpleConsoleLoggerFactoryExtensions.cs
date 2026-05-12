// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Logging
{
    internal static class SimpleConsoleLoggerFactoryExtensions
    {
        public static ILoggerFactory AddSimpleConsole(this ILoggerFactory factory, LogLevel minimalLogLevel, LogLevel minimalErrorLevel)
        {
            factory.AddProvider(new SimpleConsoleLoggerProvider(minimalLogLevel, minimalErrorLevel));
            return factory;
        }
    }
}
