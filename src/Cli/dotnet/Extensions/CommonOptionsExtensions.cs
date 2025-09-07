// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Extensions;

internal static class CommonOptionsExtensions
{
    public static LoggerVerbosity ToLoggerVerbosity(this Verbosity verbosityOptions)
    {
        LoggerVerbosity verbosity = LoggerVerbosity.Normal;
        switch (verbosityOptions)
        {
            case Verbosity.detailed:
                verbosity = LoggerVerbosity.Detailed;
                break;
            case Verbosity.diagnostic:
                verbosity = LoggerVerbosity.Diagnostic;
                break;
            case Verbosity.minimal:
                verbosity = LoggerVerbosity.Minimal;
                break;
            case Verbosity.normal:
                verbosity = LoggerVerbosity.Normal;
                break;
            case Verbosity.quiet:
                verbosity = LoggerVerbosity.Quiet;
                break;
        }
        return verbosity;
    }

    public static bool IsDetailedOrDiagnostic(this Verbosity verbosity)
    {
        return verbosity.Equals(Verbosity.diagnostic) ||
            verbosity.Equals(Verbosity.detailed);
    }

    public static bool IsQuiet(this Verbosity verbosity)
    {
        return verbosity.Equals(Verbosity.quiet);
    }
    public static bool IsMinimal(this Verbosity verbosity)
    {
        return verbosity.Equals(Verbosity.minimal);
    }
    public static bool IsNormal(this Verbosity verbosity)
    {
        return verbosity.Equals(Verbosity.normal);
    }

    /// <summary>
    /// Converts <see cref="Verbosity"/> to Microsoft.Extensions.Logging.<see cref="LogLevel"/>.
    /// </summary>
    public static LogLevel ToLogLevel(this Verbosity verbosityOptions)
    {
        LogLevel logLevel = LogLevel.Information;
        switch (verbosityOptions)
        {
            case Verbosity.detailed:
                logLevel = LogLevel.Debug;
                break;
            case Verbosity.diagnostic:
                logLevel = LogLevel.Trace;
                break;
            case Verbosity.minimal:
                logLevel = LogLevel.Error;
                break;
            case Verbosity.normal:
                logLevel = LogLevel.Information;
                break;
            case Verbosity.quiet:
                logLevel = LogLevel.None;
                break;
        }
        return logLevel;
    }
}
