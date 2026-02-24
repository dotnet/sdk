// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

/// <summary>
/// Categorizes errors for telemetry purposes.
/// </summary>
public enum ErrorCategory
{
    /// <summary>
    /// Product errors - issues we can take action on (bugs, crashes, server issues).
    /// These count against product quality metrics.
    /// </summary>
    Product,

    /// <summary>
    /// User errors - issues caused by user input or environment we can't control.
    /// Examples: invalid version, disk full, network timeout, permission denied.
    /// These are tracked separately and don't count against success rate.
    /// </summary>
    User
}

/// <summary>
/// Error info extracted from an exception for telemetry.
/// </summary>
/// <param name="ErrorType">The error type/code for telemetry.</param>
/// <param name="Category">Whether this is a product or user error.</param>
/// <param name="StatusCode">HTTP status code if applicable.</param>
/// <param name="HResult">Win32 HResult if applicable.</param>
/// <param name="Details">Additional context (no PII - sanitized values only).</param>
/// <param name="StackTrace">Full stack trace including inner exceptions.</param>
public sealed record ExceptionErrorInfo(
    string ErrorType,
    ErrorCategory Category = ErrorCategory.Product,
    int? StatusCode = null,
    int? HResult = null,
    string? Details = null,
    string? StackTrace = null);

/// <summary>
/// Maps exceptions to error info for telemetry.
/// </summary>
public static class ErrorCodeMapper
{
    /// <summary>
    /// Applies error info tags to an activity. This centralizes the tag-setting logic
    /// to avoid code duplication across progress targets and telemetry classes.
    /// </summary>
    /// <param name="activity">The activity to tag (can be null).</param>
    /// <param name="errorInfo">The error info to apply.</param>
    /// <param name="errorCode">Optional error code override.</param>
    public static void ApplyErrorTags(Activity? activity, ExceptionErrorInfo errorInfo, string? errorCode = null)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetStatus(ActivityStatusCode.Error, errorInfo.ErrorType);
        activity.SetTag("error.type", errorInfo.ErrorType);
        if (errorCode is not null)
        {
            activity.SetTag("error.code", errorCode);
        }
        activity.SetTag("error.category", errorInfo.Category.ToString().ToLowerInvariant());

        // Use pattern matching to set optional tags only if they have values
        if (errorInfo is { StatusCode: { } statusCode })
        {
            activity.SetTag("error.http_status", statusCode);
        }

        if (errorInfo is { HResult: { } hResult })
        {
            activity.SetTag("error.hresult", hResult);
        }

        if (errorInfo is { Details: { } details })
        {
            activity.SetTag("error.details", details);
        }

        if (errorInfo is { StackTrace: { } stackTrace })
        {
            activity.SetTag("error.stack_trace", stackTrace);
        }
    }

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

        var fullStackTrace = GetFullStackTrace(ex);

        return ex switch
        {
            // DotnetInstallException has specific error codes - categorize by error code
            // Sanitize the version to prevent PII leakage (user could have typed anything)
            // For network-related errors, also check the inner exception for more details
            DotnetInstallException installEx => GetInstallExceptionErrorInfo(installEx) with { StackTrace = fullStackTrace },

            // HTTP errors: 4xx client errors are often user issues, 5xx are product/server issues
            HttpRequestException httpEx => new ExceptionErrorInfo(
                httpEx.StatusCode.HasValue ? $"Http{(int)httpEx.StatusCode}" : "HttpRequestException",
                Category: ErrorCategoryClassifier.ClassifyHttpError(httpEx.StatusCode),
                StatusCode: (int?)httpEx.StatusCode,
                StackTrace: fullStackTrace),

            // FileNotFoundException before IOException (it derives from IOException)
            // Could be user error (wrong path) or product error (our code referenced wrong file)
            // Default to product since we should handle missing files gracefully
            FileNotFoundException fnfEx => new ExceptionErrorInfo(
                "FileNotFound",
                Category: ErrorCategory.Product,
                HResult: fnfEx.HResult,
                Details: fnfEx.FileName is not null ? "file_specified" : null,
                StackTrace: fullStackTrace),

            // Permission denied - user environment issue (needs elevation or different permissions)
            UnauthorizedAccessException => new ExceptionErrorInfo(
                "PermissionDenied",
                Category: ErrorCategory.User,
                StackTrace: fullStackTrace),

            // Directory not found - could be user specified bad path
            DirectoryNotFoundException => new ExceptionErrorInfo(
                "DirectoryNotFound",
                Category: ErrorCategory.User,
                StackTrace: fullStackTrace),

            IOException ioEx => MapIOException(ioEx, fullStackTrace),

            // User cancelled the operation
            OperationCanceledException => new ExceptionErrorInfo(
                "Cancelled",
                Category: ErrorCategory.User,
                StackTrace: fullStackTrace),

            // Invalid argument - user provided bad input
            ArgumentException argEx => new ExceptionErrorInfo(
                "InvalidArgument",
                Category: ErrorCategory.User,
                Details: argEx.ParamName,
                StackTrace: fullStackTrace),

            // Invalid operation - usually a bug in our code
            InvalidOperationException => new ExceptionErrorInfo(
                "InvalidOperation",
                Category: ErrorCategory.Product,
                StackTrace: fullStackTrace),

            // Not supported - could be user trying unsupported scenario
            NotSupportedException => new ExceptionErrorInfo(
                "NotSupported",
                Category: ErrorCategory.User,
                StackTrace: fullStackTrace),

            // Timeout - network/environment issue outside our control
            TimeoutException => new ExceptionErrorInfo(
                "Timeout",
                Category: ErrorCategory.User,
                StackTrace: fullStackTrace),

            // Unknown exceptions default to product (fail-safe - we should handle known cases)
            _ => new ExceptionErrorInfo(
                ex.GetType().Name,
                Category: ErrorCategory.Product,
                StackTrace: fullStackTrace)
        };
    }

    /// <summary>
    /// Gets error info for a DotnetInstallException, enriching with inner exception details
    /// for network-related and IO-related errors.
    /// </summary>
    private static ExceptionErrorInfo GetInstallExceptionErrorInfo(
        DotnetInstallException installEx)
    {
        var errorCode = installEx.ErrorCode;
        var baseCategory = ErrorCategoryClassifier.ClassifyInstallError(errorCode);
        var details = installEx.Version is not null ? VersionSanitizer.Sanitize(installEx.Version) : null;
        int? httpStatus = null;

        if (ErrorCategoryClassifier.IsNetworkRelatedErrorCode(errorCode) && installEx.InnerException is not null)
        {
            var (refinedCategory, innerHttpStatus, innerDetails) = ErrorCategoryClassifier.AnalyzeNetworkException(installEx.InnerException);
            baseCategory = refinedCategory;
            httpStatus = innerHttpStatus;

            if (innerDetails is not null)
            {
                details = details is not null ? $"{details};{innerDetails}" : innerDetails;
            }
        }

        if (ErrorCategoryClassifier.IsIORelatedErrorCode(errorCode) && installEx.InnerException is IOException ioInner)
        {
            var (_, ioCategory, ioDetails) = ErrorCategoryClassifier.ClassifyIOErrorByHResult(ioInner.HResult);
            baseCategory = ioCategory;

            if (ioDetails is not null)
            {
                details = details is not null ? $"{details};{ioDetails}" : ioDetails;
            }
        }

        return new ExceptionErrorInfo(
            errorCode.ToString(),
            Category: baseCategory,
            StatusCode: httpStatus,
            Details: details);
    }

    private static ExceptionErrorInfo MapIOException(IOException ioEx, string? stackTrace)
    {
        // Delegate to the single-lookup classifier to avoid duplicating HResult→category logic
        var (errorType, category, details) = ErrorCategoryClassifier.ClassifyIOErrorByHResult(ioEx.HResult);

        return new ExceptionErrorInfo(
            errorType,
            Category: category,
            HResult: ioEx.HResult,
            Details: details,
            StackTrace: stackTrace);
    }

    /// <summary>
    /// Builds a full stack trace string including inner exception types and their stack traces.
    /// Exception messages are not included because they may contain user-provided input.
    /// </summary>
    private static string? GetFullStackTrace(Exception ex)
    {
        try
        {
            var sb = new StringBuilder();
            if (ex.StackTrace is { } trace)
            {
                sb.Append(trace);
            }

            var inner = ex.InnerException;
            // Limit depth to prevent infinite loops and overly long strings
            const int maxDepth = 10;
            var depth = 0;
            while (inner != null && depth < maxDepth)
            {
                sb.AppendLine();
                sb.AppendLine($"Inner Exception: {inner.GetType().FullName}");
                if (inner.StackTrace is { } innerTrace)
                {
                    sb.Append(innerTrace);
                }
                inner = inner.InnerException;
                depth++;
            }

            return sb.Length > 0 ? sb.ToString() : null;
        }
        catch
        {
            // Never fail telemetry due to stack trace parsing
            return null;
        }
    }
}
