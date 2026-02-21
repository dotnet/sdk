// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

/// <summary>
/// Maps exceptions to <see cref="ExceptionErrorInfo"/> for telemetry.
/// Each exception type is classified with a telemetry-safe error type,
/// a <see cref="ErrorCategory"/>, and optional PII-free details.
/// </summary>
internal static class ExceptionErrorMapper
{
    /// <summary>
    /// Builds an <see cref="ExceptionErrorInfo"/> from the given exception,
    /// enriching it with source location and exception chain metadata.
    /// </summary>
    internal static ExceptionErrorInfo Map(Exception ex)
    {
        // Unwrap single-inner AggregateExceptions
        if (ex is AggregateException { InnerExceptions.Count: 1 } aggEx)
        {
            return Map(aggEx.InnerExceptions[0]);
        }

        // If it's a plain Exception wrapper, use the inner exception for better error type
        if (ex.GetType() == typeof(Exception) && ex.InnerException is not null)
        {
            return Map(ex.InnerException);
        }

        // Get common enrichment data
        var sourceLocation = ExceptionInspector.GetSafeSourceLocation(ex);
        var exceptionChain = ExceptionInspector.GetExceptionChain(ex);

        return ex switch
        {
            // DotnetInstallException has specific error codes — categorize by error code.
            // Sanitize the version to prevent PII leakage (user could have typed anything).
            // For network-related errors, also check the inner exception for more details.
            DotnetInstallException installEx => MapInstallException(installEx, sourceLocation, exceptionChain),

            // HTTP errors: 4xx client errors are often user issues, 5xx are product/server issues
            HttpRequestException httpEx => new ExceptionErrorInfo(
                httpEx.StatusCode.HasValue ? $"Http{(int)httpEx.StatusCode}" : "HttpRequestException",
                Category: ErrorCategoryClassifier.ClassifyHttpError(httpEx.StatusCode),
                StatusCode: (int?)httpEx.StatusCode,
                SourceLocation: sourceLocation,
                ExceptionChain: exceptionChain),

            // FileNotFoundException before IOException (it derives from IOException).
            // Could be user error (wrong path) or product error (our code referenced wrong file).
            // Default to product since we should handle missing files gracefully.
            FileNotFoundException fnfEx => new ExceptionErrorInfo(
                "FileNotFound",
                Category: ErrorCategory.Product,
                HResult: fnfEx.HResult,
                Details: fnfEx.FileName is not null ? "file_specified" : null,
                SourceLocation: sourceLocation,
                ExceptionChain: exceptionChain),

            // Permission denied — user environment issue (needs elevation or different permissions)
            UnauthorizedAccessException => new ExceptionErrorInfo(
                "PermissionDenied",
                Category: ErrorCategory.User,
                SourceLocation: sourceLocation,
                ExceptionChain: exceptionChain),

            // Directory not found — could be user specified bad path
            DirectoryNotFoundException => new ExceptionErrorInfo(
                "DirectoryNotFound",
                Category: ErrorCategory.User,
                SourceLocation: sourceLocation,
                ExceptionChain: exceptionChain),

            IOException ioEx => MapIOException(ioEx, sourceLocation, exceptionChain),

            // User cancelled the operation
            OperationCanceledException => new ExceptionErrorInfo(
                "Cancelled",
                Category: ErrorCategory.User,
                SourceLocation: sourceLocation,
                ExceptionChain: exceptionChain),

            // Invalid argument — likely a bug in our code (arguments are set programmatically)
            ArgumentException argEx => new ExceptionErrorInfo(
                "InvalidArgument",
                Category: ErrorCategory.Product,
                Details: argEx.ParamName,
                SourceLocation: sourceLocation,
                ExceptionChain: exceptionChain),

            // Invalid operation — usually a bug in our code
            InvalidOperationException => new ExceptionErrorInfo(
                "InvalidOperation",
                Category: ErrorCategory.Product,
                SourceLocation: sourceLocation,
                ExceptionChain: exceptionChain),

            // Not supported — likely a product issue (missing implementation or unsupported code path)
            NotSupportedException => new ExceptionErrorInfo(
                "NotSupported",
                Category: ErrorCategory.Product,
                SourceLocation: sourceLocation,
                ExceptionChain: exceptionChain),

            // Timeout — network/environment issue outside our control
            TimeoutException => new ExceptionErrorInfo(
                "Timeout",
                Category: ErrorCategory.User,
                SourceLocation: sourceLocation,
                ExceptionChain: exceptionChain),

            // Unknown exceptions default to product (fail-safe — we should handle known cases)
            _ => new ExceptionErrorInfo(
                ex.GetType().Name,
                Category: ErrorCategory.Product,
                SourceLocation: sourceLocation,
                ExceptionChain: exceptionChain)
        };
    }

    /// <summary>
    /// Maps a <see cref="DotnetInstallException"/>, enriching with inner exception details
    /// for network-related errors.
    /// </summary>
    private static ExceptionErrorInfo MapInstallException(
        DotnetInstallException installEx,
        string? sourceLocation,
        string? exceptionChain)
    {
        var errorCode = installEx.ErrorCode;
        var baseCategory = ErrorCategoryClassifier.ClassifyInstallError(errorCode);
        var details = installEx.Version is not null ? VersionSanitizer.Sanitize(installEx.Version) : null;
        int? httpStatus = null;

        // For network-related errors, check the inner exception to better categorize
        // and extract additional diagnostic info
        if (NetworkErrorAnalyzer.IsNetworkRelatedErrorCode(errorCode) && installEx.InnerException is not null)
        {
            var (refinedCategory, innerHttpStatus, innerDetails) = NetworkErrorAnalyzer.AnalyzeNetworkException(installEx.InnerException);
            baseCategory = refinedCategory;
            httpStatus = innerHttpStatus;

            // Combine details: version + inner exception info
            if (innerDetails is not null)
            {
                details = details is not null ? $"{details};{innerDetails}" : innerDetails;
            }
        }

        return new ExceptionErrorInfo(
            errorCode.ToString(),
            Category: baseCategory,
            StatusCode: httpStatus,
            Details: details,
            SourceLocation: sourceLocation,
            ExceptionChain: exceptionChain);
    }

    /// <summary>
    /// Maps a generic <see cref="IOException"/> using its HResult.
    /// </summary>
    private static ExceptionErrorInfo MapIOException(IOException ioEx, string? sourceLocation, string? exceptionChain)
    {
        var (errorType, details) = HResultMapper.GetErrorTypeFromHResult(ioEx.HResult);
        var category = ErrorCategoryClassifier.ClassifyIOError(errorType);

        return new ExceptionErrorInfo(
            errorType,
            Category: category,
            HResult: ioEx.HResult,
            Details: details,
            SourceLocation: sourceLocation,
            ExceptionChain: exceptionChain);
    }
}
