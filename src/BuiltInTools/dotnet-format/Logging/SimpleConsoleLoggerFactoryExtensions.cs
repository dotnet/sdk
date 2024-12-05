// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.CommandLine;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Logging
{
    internal static class SimpleConsoleLoggerFactoryExtensions
    {
        public static ILoggerFactory AddSimpleConsole(this ILoggerFactory factory, IConsole console, LogLevel minimalLogLevel, LogLevel minimalErrorLevel)
        {
            factory.AddProvider(new SimpleConsoleLoggerProvider(console, minimalLogLevel, minimalErrorLevel));
            return factory;
        }
    }
}
