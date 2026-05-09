// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
