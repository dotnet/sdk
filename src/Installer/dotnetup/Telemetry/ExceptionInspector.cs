// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

/// <summary>
/// Extracts PII-safe diagnostic metadata from exceptions for telemetry:
/// full stack traces (without exception messages) and exception-type chains.
/// </summary>
/// <remarks>
/// Follows the same pattern as the .NET SDK's <c>TelemetryFilter.ExceptionToStringWithoutMessage</c>.
/// Exception messages are stripped because they can contain user-provided input (file paths,
/// version strings, etc.), but stack traces are safe — especially in NativeAOT where they
/// contain only method names without source file paths or line numbers.
/// </remarks>
internal static class ExceptionInspector
{
    /// <summary>
    /// Gets the full exception details without messages, following the SDK pattern.
    /// Includes exception type names, full stack traces, and inner exception chains.
    /// Messages are stripped to avoid PII; stack traces are kept as they only contain
    /// type/method names (especially safe in NativeAOT).
    /// </summary>
    internal static string? GetStackTraceWithoutMessage(Exception ex)
    {
        try
        {
            return ExceptionToStringWithoutMessage(ex);
        }
        catch
        {
            // Never fail telemetry due to stack trace parsing
            return null;
        }
    }

    /// <summary>
    /// Gets the exception type chain for wrapped exceptions.
    /// Example: "HttpRequestException->SocketException"
    /// </summary>
    internal static string? GetExceptionChain(Exception ex)
    {
        if (ex.InnerException == null)
        {
            return null;
        }

        try
        {
            var types = new List<string> { ex.GetType().Name };
            var inner = ex.InnerException;

            // Limit depth to prevent infinite loops and overly long strings
            const int maxDepth = 5;
            var depth = 0;

            while (inner != null && depth < maxDepth)
            {
                types.Add(inner.GetType().Name);
                inner = inner.InnerException;
                depth++;
            }

            return string.Join("->", types);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Converts an exception to string without its message, following the .NET SDK pattern.
    /// For AggregateExceptions, recursively processes all inner exceptions.
    /// </summary>
    private static string ExceptionToStringWithoutMessage(Exception e)
    {
        if (e is AggregateException aggregate)
        {
            var text = NonAggregateExceptionToStringWithoutMessage(aggregate);

            for (int i = 0; i < aggregate.InnerExceptions.Count; i++)
            {
                text = string.Format("{0}{1}---> (Inner Exception #{2}) {3}{4}{5}",
                    text,
                    Environment.NewLine,
                    i,
                    ExceptionToStringWithoutMessage(aggregate.InnerExceptions[i]),
                    "<---",
                    Environment.NewLine);
            }

            return text;
        }

        return NonAggregateExceptionToStringWithoutMessage(e);
    }

    /// <summary>
    /// Converts a non-aggregate exception to string: type name + inner exceptions + stack trace.
    /// Messages are intentionally omitted to avoid PII.
    /// </summary>
    private static string NonAggregateExceptionToStringWithoutMessage(Exception e)
    {
        const string EndOfInnerExceptionStackTrace = "--- End of inner exception stack trace ---";

        var s = e.GetType().ToString();

        if (e.InnerException != null)
        {
            s = s + " ---> " + ExceptionToStringWithoutMessage(e.InnerException) + Environment.NewLine +
                "   " + EndOfInnerExceptionStackTrace;
        }

        var stackTrace = e.StackTrace;
        if (stackTrace != null)
        {
            s += Environment.NewLine + stackTrace;
        }

        return s;
    }
}
