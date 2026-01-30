// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using Microsoft.Dotnet.Installation;

namespace Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

/// <summary>
/// Error info extracted from an exception for telemetry.
/// </summary>
/// <param name="ErrorType">The error type/code for telemetry.</param>
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

        // If it's a plain Exception wrapper, use the inner exception for better error type
        if (ex.GetType() == typeof(Exception) && ex.InnerException is not null)
        {
            return GetErrorInfo(ex.InnerException);
        }

        return ex switch
        {
            // DotnetInstallException has specific error codes
            DotnetInstallException installEx => new ExceptionErrorInfo(
                installEx.ErrorCode.ToString(),
                Details: installEx.Version),

            HttpRequestException httpEx => new ExceptionErrorInfo(
                httpEx.StatusCode.HasValue ? $"Http{(int)httpEx.StatusCode}" : "HttpRequestException",
                StatusCode: (int?)httpEx.StatusCode),

            // FileNotFoundException before IOException (it derives from IOException)
            FileNotFoundException fnfEx => new ExceptionErrorInfo(
                "FileNotFound",
                HResult: fnfEx.HResult,
                Details: fnfEx.FileName is not null ? "file_specified" : null),

            UnauthorizedAccessException => new ExceptionErrorInfo("PermissionDenied"),

            DirectoryNotFoundException => new ExceptionErrorInfo("DirectoryNotFound"),

            IOException ioEx => MapIOException(ioEx),

            OperationCanceledException => new ExceptionErrorInfo("Cancelled"),

            ArgumentException argEx => new ExceptionErrorInfo(
                "InvalidArgument",
                Details: argEx.ParamName),

            _ => new ExceptionErrorInfo(ex.GetType().Name)
        };
    }

    private static ExceptionErrorInfo MapIOException(IOException ioEx)
    {
        // Check for common HResult values
        const int ERROR_DISK_FULL = unchecked((int)0x80070070);
        const int ERROR_HANDLE_DISK_FULL = unchecked((int)0x80070027);
        const int ERROR_ACCESS_DENIED = unchecked((int)0x80070005);

        return ioEx.HResult switch
        {
            ERROR_DISK_FULL or ERROR_HANDLE_DISK_FULL => new ExceptionErrorInfo("DiskFull", HResult: ioEx.HResult),
            ERROR_ACCESS_DENIED => new ExceptionErrorInfo("PermissionDenied", HResult: ioEx.HResult),
            _ => new ExceptionErrorInfo("IOException", HResult: ioEx.HResult)
        };
    }
}
