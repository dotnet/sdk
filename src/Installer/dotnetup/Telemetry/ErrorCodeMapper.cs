// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;

namespace Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

/// <summary>
/// Error info extracted from an exception for telemetry.
/// </summary>
/// <param name="ErrorType">The exception type name.</param>
/// <param name="StatusCode">HTTP status code if applicable.</param>
/// <param name="HResult">Win32 HResult if applicable.</param>
/// <param name="Details">Additional context like file path.</param>
public sealed record ExceptionErrorInfo(
    string ErrorType,
    int? StatusCode = null,
    int? HResult = null,
    string? Details = null);

/// <summary>
/// Maps exceptions to error info for telemetry.
/// </summary>
public static class ErrorCodeMapper
{
    /// <summary>
    /// Extracts error info from an exception.
    /// </summary>
    /// <param name="ex">The exception to analyze.</param>
    /// <returns>Error info with type name and contextual details.</returns>
    public static ExceptionErrorInfo GetErrorInfo(Exception ex)
    {
        // Unwrap single-inner AggregateExceptions
        if (ex is AggregateException { InnerExceptions.Count: 1 } aggEx)
        {
            return GetErrorInfo(aggEx.InnerExceptions[0]);
        }

        var typeName = ex.GetType().Name;

        return ex switch
        {
            HttpRequestException httpEx => new ExceptionErrorInfo(
                typeName,
                StatusCode: (int?)httpEx.StatusCode),

            // FileNotFoundException before IOException (it derives from IOException)
            FileNotFoundException fnfEx => new ExceptionErrorInfo(
                typeName,
                HResult: fnfEx.HResult,
                Details: fnfEx.FileName is not null ? "file_specified" : null),

            IOException ioEx => new ExceptionErrorInfo(
                typeName,
                HResult: ioEx.HResult),

            _ => new ExceptionErrorInfo(typeName)
        };
    }
}
