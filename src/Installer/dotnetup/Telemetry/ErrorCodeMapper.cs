// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

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
/// <param name="SourceLocation">Method name from our code where error occurred (includes file basename and line).</param>
/// <param name="ExceptionChain">Chain of exception types for wrapped exceptions.</param>
/// <param name="StackTrace">Full stack trace (safe to include - contains no PII in NativeAOT).</param>
/// <param name="ThrowSite">File name and line number where the exception was originally thrown (e.g., "DotnetArchiveExtractor.cs:139").</param>
public sealed record ExceptionErrorInfo(
    string ErrorType,
    ErrorCategory Category = ErrorCategory.Product,
    int? StatusCode = null,
    int? HResult = null,
    string? Details = null,
    string? SourceLocation = null,
    string? ExceptionChain = null,
    string? StackTrace = null,
    string? ThrowSite = null);

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
        if (activity is null) return;

        activity.SetStatus(ActivityStatusCode.Error, errorInfo.ErrorType);
        activity.SetTag("error.type", errorInfo.ErrorType);
        if (errorCode is not null)
        {
            activity.SetTag("error.code", errorCode);
        }
        activity.SetTag("error.category", errorInfo.Category.ToString().ToLowerInvariant());

        // Use pattern matching to set optional tags only if they have values
        if (errorInfo is { StatusCode: { } statusCode })
            activity.SetTag("error.http_status", statusCode);
        if (errorInfo is { HResult: { } hResult })
            activity.SetTag("error.hresult", hResult);
        if (errorInfo is { Details: { } details })
            activity.SetTag("error.details", details);
        if (errorInfo is { SourceLocation: { } sourceLocation })
            activity.SetTag("error.source_location", sourceLocation);
        if (errorInfo is { ExceptionChain: { } exceptionChain })
            activity.SetTag("error.exception_chain", exceptionChain);
        if (errorInfo is { StackTrace: { } stackTrace })
            activity.SetTag("error.stack_trace", stackTrace);
        if (errorInfo is { ThrowSite: { } throwSite })
            activity.SetTag("error.throw_site", throwSite);

        // NOTE: We intentionally do NOT call activity.RecordException(ex)
        // because exception messages/stacks can contain PII
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

        // Get common enrichment data (single stack walk for all three values)
        var exceptionChain = GetExceptionChain(ex);
        var (sourceLocation, safeStackTrace, throwSite) = GetStackInfo(ex);

        return ex switch
        {
            // DotnetInstallException has specific error codes - categorize by error code
            // Sanitize the version to prevent PII leakage (user could have typed anything)
            // For network-related errors, also check the inner exception for more details
            DotnetInstallException installEx => GetInstallExceptionErrorInfo(installEx, sourceLocation, exceptionChain) with { StackTrace = safeStackTrace, ThrowSite = throwSite },

            // HTTP errors: 4xx client errors are often user issues, 5xx are product/server issues
            HttpRequestException httpEx => new ExceptionErrorInfo(
                httpEx.StatusCode.HasValue ? $"Http{(int)httpEx.StatusCode}" : "HttpRequestException",
                Category: GetHttpErrorCategory(httpEx.StatusCode),
                StatusCode: (int?)httpEx.StatusCode,
                SourceLocation: sourceLocation,
                ExceptionChain: exceptionChain,
                StackTrace: safeStackTrace,
                ThrowSite: throwSite),

            // FileNotFoundException before IOException (it derives from IOException)
            // Could be user error (wrong path) or product error (our code referenced wrong file)
            // Default to product since we should handle missing files gracefully
            FileNotFoundException fnfEx => new ExceptionErrorInfo(
                "FileNotFound",
                Category: ErrorCategory.Product,
                HResult: fnfEx.HResult,
                Details: fnfEx.FileName is not null ? "file_specified" : null,
                SourceLocation: sourceLocation,
                ExceptionChain: exceptionChain,
                StackTrace: safeStackTrace,
                ThrowSite: throwSite),

            // Permission denied - user environment issue (needs elevation or different permissions)
            UnauthorizedAccessException => new ExceptionErrorInfo(
                "PermissionDenied",
                Category: ErrorCategory.User,
                SourceLocation: sourceLocation,
                ExceptionChain: exceptionChain,
                StackTrace: safeStackTrace,
                ThrowSite: throwSite),

            // Directory not found - could be user specified bad path
            DirectoryNotFoundException => new ExceptionErrorInfo(
                "DirectoryNotFound",
                Category: ErrorCategory.User,
                SourceLocation: sourceLocation,
                ExceptionChain: exceptionChain,
                StackTrace: safeStackTrace,
                ThrowSite: throwSite),

            IOException ioEx => MapIOException(ioEx, sourceLocation, exceptionChain, safeStackTrace, throwSite),

            // User cancelled the operation
            OperationCanceledException => new ExceptionErrorInfo(
                "Cancelled",
                Category: ErrorCategory.User,
                SourceLocation: sourceLocation,
                ExceptionChain: exceptionChain,
                StackTrace: safeStackTrace,
                ThrowSite: throwSite),

            // Invalid argument - user provided bad input
            ArgumentException argEx => new ExceptionErrorInfo(
                "InvalidArgument",
                Category: ErrorCategory.User,
                Details: argEx.ParamName,
                SourceLocation: sourceLocation,
                ExceptionChain: exceptionChain,
                StackTrace: safeStackTrace,
                ThrowSite: throwSite),

            // Invalid operation - usually a bug in our code
            InvalidOperationException => new ExceptionErrorInfo(
                "InvalidOperation",
                Category: ErrorCategory.Product,
                SourceLocation: sourceLocation,
                ExceptionChain: exceptionChain,
                StackTrace: safeStackTrace,
                ThrowSite: throwSite),

            // Not supported - could be user trying unsupported scenario
            NotSupportedException => new ExceptionErrorInfo(
                "NotSupported",
                Category: ErrorCategory.User,
                SourceLocation: sourceLocation,
                ExceptionChain: exceptionChain,
                StackTrace: safeStackTrace,
                ThrowSite: throwSite),

            // Timeout - network/environment issue outside our control
            TimeoutException => new ExceptionErrorInfo(
                "Timeout",
                Category: ErrorCategory.User,
                SourceLocation: sourceLocation,
                ExceptionChain: exceptionChain,
                StackTrace: safeStackTrace,
                ThrowSite: throwSite),

            // Unknown exceptions default to product (fail-safe - we should handle known cases)
            _ => new ExceptionErrorInfo(
                ex.GetType().Name,
                Category: ErrorCategory.Product,
                SourceLocation: sourceLocation,
                ExceptionChain: exceptionChain,
                StackTrace: safeStackTrace,
                ThrowSite: throwSite)
        };
    }

    /// <summary>
    /// Gets the error category for a DotnetInstallErrorCode.
    /// </summary>
    private static ErrorCategory GetInstallErrorCategory(DotnetInstallErrorCode errorCode)
    {
        return errorCode switch
        {
            // User errors - bad input or environment issues
            DotnetInstallErrorCode.VersionNotFound => ErrorCategory.User,      // User typed invalid version
            DotnetInstallErrorCode.ReleaseNotFound => ErrorCategory.User,      // User requested non-existent release
            DotnetInstallErrorCode.InvalidChannel => ErrorCategory.User,       // User provided bad channel format
            DotnetInstallErrorCode.PermissionDenied => ErrorCategory.User,     // User needs to elevate/fix permissions
            DotnetInstallErrorCode.DiskFull => ErrorCategory.User,             // User's disk is full
            DotnetInstallErrorCode.NetworkError => ErrorCategory.User,         // User's network issue

            // Product errors - issues we can take action on
            DotnetInstallErrorCode.ExtractionFailed => ErrorCategory.Product,  // Our extraction code issue (inner IOException classified separately)
            DotnetInstallErrorCode.NoMatchingReleaseFileForPlatform => ErrorCategory.Product,    // Our manifest/logic issue
            DotnetInstallErrorCode.DownloadFailed => ErrorCategory.Product,    // Server or download logic issue
            DotnetInstallErrorCode.HashMismatch => ErrorCategory.Product,      // Corrupted download or server issue
            DotnetInstallErrorCode.ManifestFetchFailed => ErrorCategory.Product, // Server unreachable or CDN issue
            DotnetInstallErrorCode.ManifestParseFailed => ErrorCategory.Product, // Bad manifest or our parsing bug
            DotnetInstallErrorCode.ArchiveCorrupted => ErrorCategory.Product,  // Bad archive from server or download
            DotnetInstallErrorCode.InstallationLocked => ErrorCategory.Product, // Our locking mechanism issue
            DotnetInstallErrorCode.LocalManifestError => ErrorCategory.Product, // File system issue with our manifest
            DotnetInstallErrorCode.LocalManifestCorrupted => ErrorCategory.Product, // Our manifest is corrupt - we should handle
            DotnetInstallErrorCode.Unknown => ErrorCategory.Product,           // Unknown = assume product issue

            _ => ErrorCategory.Product  // Default to product for new codes
        };
    }

    /// <summary>
    /// Gets error info for a DotnetInstallException, enriching with inner exception details
    /// for network-related errors.
    /// </summary>
    private static ExceptionErrorInfo GetInstallExceptionErrorInfo(
        DotnetInstallException installEx,
        string? sourceLocation,
        string? exceptionChain)
    {
        var errorCode = installEx.ErrorCode;
        var baseCategory = GetInstallErrorCategory(errorCode);
        var details = installEx.Version is not null ? VersionSanitizer.Sanitize(installEx.Version) : null;
        int? httpStatus = null;

        // For network-related errors, check the inner exception to better categorize
        // and extract additional diagnostic info
        if (IsNetworkRelatedErrorCode(errorCode) && installEx.InnerException is not null)
        {
            var (refinedCategory, innerHttpStatus, innerDetails) = AnalyzeNetworkException(installEx.InnerException);
            baseCategory = refinedCategory;
            httpStatus = innerHttpStatus;

            // Combine details: version + inner exception info
            if (innerDetails is not null)
            {
                details = details is not null ? $"{details};{innerDetails}" : innerDetails;
            }
        }

        // For extraction errors, check if the inner exception is an IOException and classify
        // by HResult using the existing ErrorCategoryClassifier. This avoids duplicating
        // HResult→error-type logic in the extraction layer.
        if (IsIORelatedErrorCode(errorCode) && installEx.InnerException is IOException ioInner)
        {
            var (ioErrorType, ioCategory, ioDetails) = ErrorCategoryClassifier.ClassifyIOErrorByHResult(ioInner.HResult);
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
            Details: details,
            SourceLocation: sourceLocation,
            ExceptionChain: exceptionChain);
    }

    /// <summary>
    /// Checks if the error code is related to network operations.
    /// </summary>
    private static bool IsNetworkRelatedErrorCode(DotnetInstallErrorCode errorCode)
    {
        return errorCode is
            DotnetInstallErrorCode.ManifestFetchFailed or
            DotnetInstallErrorCode.DownloadFailed or
            DotnetInstallErrorCode.NetworkError;
    }

    /// <summary>
    /// Checks if the error code is related to IO operations where the inner exception
    /// may be an IOException that should be classified by HResult.
    /// </summary>
    private static bool IsIORelatedErrorCode(DotnetInstallErrorCode errorCode)
    {
        return errorCode is
            DotnetInstallErrorCode.ExtractionFailed or
            DotnetInstallErrorCode.LocalManifestError;
    }

    /// <summary>
    /// Analyzes a network-related inner exception to determine the category and extract details.
    /// </summary>
    private static (ErrorCategory Category, int? HttpStatus, string? Details) AnalyzeNetworkException(Exception inner)
    {
        // Walk the exception chain to find HttpRequestException or SocketException
        // Look for the most specific info we can find
        HttpRequestException? foundHttpEx = null;
        SocketException? foundSocketEx = null;

        var current = inner;
        while (current is not null)
        {
            if (current is HttpRequestException httpEx && foundHttpEx is null)
            {
                foundHttpEx = httpEx;
            }

            if (current is SocketException socketEx && foundSocketEx is null)
            {
                foundSocketEx = socketEx;
            }

            current = current.InnerException;
        }

        // Prefer socket-level info if available (more specific)
        if (foundSocketEx is not null)
        {
            var socketErrorName = foundSocketEx.SocketErrorCode.ToString().ToLowerInvariant();
            return (ErrorCategory.User, null, $"socket_{socketErrorName}");
        }

        // Then HTTP-level info
        if (foundHttpEx is not null)
        {
            var category = GetHttpErrorCategory(foundHttpEx.StatusCode);
            var httpStatus = (int?)foundHttpEx.StatusCode;

            string? details = null;
            if (foundHttpEx.StatusCode.HasValue)
            {
                details = $"http_{(int)foundHttpEx.StatusCode}";
            }
            else if (foundHttpEx.HttpRequestError != HttpRequestError.Unknown)
            {
                // .NET 7+ has HttpRequestError enum for non-HTTP failures
                details = $"request_error_{foundHttpEx.HttpRequestError.ToString().ToLowerInvariant()}";
            }

            return (category, httpStatus, details);
        }

        // Couldn't determine from inner exception - use default Product category
        // but mark as unknown network error
        return (ErrorCategory.Product, null, "network_unknown");
    }

    /// <summary>
    /// Gets the error category for an HTTP status code.
    /// </summary>
    private static ErrorCategory GetHttpErrorCategory(HttpStatusCode? statusCode)
    {
        if (!statusCode.HasValue)
        {
            // No status code usually means network failure - user environment
            return ErrorCategory.User;
        }

        var code = (int)statusCode.Value;
        return code switch
        {
            >= 500 => ErrorCategory.Product,  // 5xx server errors - our infrastructure
            404 => ErrorCategory.User,        // Not found - likely user requested invalid resource
            403 => ErrorCategory.User,        // Forbidden - user environment/permission issue
            401 => ErrorCategory.User,        // Unauthorized - user auth issue
            408 => ErrorCategory.User,        // Request timeout - user network
            429 => ErrorCategory.User,        // Too many requests - user hitting rate limits
            _ => ErrorCategory.Product        // Other 4xx - likely our bug (bad request format, etc.)
        };
    }

    private static ExceptionErrorInfo MapIOException(IOException ioEx, string? sourceLocation, string? exceptionChain, string? stackTrace, string? throwSite)
    {
        // Delegate to the single-lookup classifier to avoid duplicating HResult→category logic
        var (errorType, category, details) = ErrorCategoryClassifier.ClassifyIOErrorByHResult(ioEx.HResult);

        return new ExceptionErrorInfo(
            errorType,
            Category: category,
            HResult: ioEx.HResult,
            Details: details,
            SourceLocation: sourceLocation,
            ExceptionChain: exceptionChain,
            StackTrace: stackTrace,
            ThrowSite: throwSite);
    }



    /// <summary>
    /// Extracts source location, safe stack trace, and throw site from an exception
    /// in a single stack walk. This replaces three separate methods that each created
    /// their own StackTrace and walked the frames independently.
    /// </summary>
    /// <returns>
    /// A tuple of:
    /// - SourceLocation: first frame in our code (where we called into BCL that threw)
    /// - StackTrace: all frames from our namespaces joined with " -> "
    /// - ThrowSite: "FileName.cs:42" from the deepest frame (or first owned frame with file info)
    /// </returns>
    private static (string? SourceLocation, string? StackTrace, string? ThrowSite) GetStackInfo(Exception ex)
    {
        try
        {
            var stackTrace = new StackTrace(ex, fNeedFileInfo: true);
            var frames = stackTrace.GetFrames();

            if (frames == null || frames.Length == 0)
            {
                return (null, null, null);
            }

            string? sourceLocation = null;
            string? throwSite = null;
            string? bclFallbackLocation = null;
            var safeFrames = new List<string>();

            for (int i = 0; i < frames.Length; i++)
            {
                var frame = frames[i];
                var methodInfo = DiagnosticMethodInfo.Create(frame);
                if (methodInfo == null) continue;

                var declaringType = methodInfo.DeclaringTypeName;
                if (string.IsNullOrEmpty(declaringType)) continue;

                var typeName = ExtractTypeName(declaringType);

                // Throw site: get file:line from deepest frame (i == 0), falling back to first owned frame with file info
                if (i == 0)
                {
                    var fileName = GetSafeFileName(frame);
                    var lineNumber = frame.GetFileLineNumber();
                    if (fileName != null && lineNumber > 0)
                    {
                        throwSite = $"{fileName}:{lineNumber}";
                    }
                }

                // Source location fallback: first frame of any kind (BCL prefix)
                if (bclFallbackLocation == null)
                {
                    bclFallbackLocation = $"[BCL]{typeName}.{methodInfo.Name}";
                }

                if (IsOwnedNamespace(declaringType))
                {
                    var location = FormatFrameLocation(typeName, methodInfo.Name, frame);
                    safeFrames.Add(location);

                    // Source location: first owned frame
                    sourceLocation ??= location;

                    // Throw site fallback: first owned frame with file info
                    if (throwSite == null)
                    {
                        var fn = GetSafeFileName(frame);
                        var ln = frame.GetFileLineNumber();
                        if (fn != null && ln > 0)
                        {
                            throwSite = $"{fn}:{ln}";
                        }
                    }
                }
            }

            // If no owned frame found, use the BCL fallback for source location
            sourceLocation ??= bclFallbackLocation;

            var traceString = safeFrames.Count > 0 ? string.Join(" -> ", safeFrames) : null;
            return (sourceLocation, traceString, throwSite);
        }
        catch
        {
            // Never fail telemetry due to stack trace parsing
            return (null, null, null);
        }
    }

    /// <summary>
    /// Formats a stack frame location as "FileName.cs:TypeName.Method:42" or "TypeName.Method:42".
    /// Includes the source file basename when available for quick identification.
    /// </summary>
    private static string FormatFrameLocation(string typeName, string methodName, StackFrame frame)
    {
        var lineNumber = frame.GetFileLineNumber();
        var fileName = GetSafeFileName(frame);

        var location = fileName != null
            ? $"{fileName}:{typeName}.{methodName}"
            : $"{typeName}.{methodName}";

        if (lineNumber > 0)
        {
            location += $":{lineNumber}";
        }

        return location;
    }

    /// <summary>
    /// Extracts just the file name (no path) from a stack frame.
    /// Returns null if no file info is available.
    /// File names from our own build are safe (not PII) — they're paths from the build machine.
    /// </summary>
    private static string? GetSafeFileName(StackFrame frame)
    {
        var filePath = frame.GetFileName();
        if (string.IsNullOrEmpty(filePath))
        {
            return null;
        }

        // Strip to basename only — no directory paths
        return Path.GetFileName(filePath);
    }

    /// <summary>
    /// Checks if a type name belongs to one of our owned namespaces.
    /// </summary>
    private static bool IsOwnedNamespace(string declaringType)
    {
        return declaringType.StartsWith("Microsoft.DotNet.Tools.Bootstrapper", StringComparison.Ordinal) ||
               declaringType.StartsWith("Microsoft.Dotnet.Installation", StringComparison.Ordinal);
    }

    /// <summary>
    /// Extracts just the type name from a fully qualified type name.
    /// </summary>
    private static string ExtractTypeName(string fullTypeName)
    {
        var typeName = fullTypeName;
        var lastDot = typeName.LastIndexOf('.');
        if (lastDot >= 0)
        {
            typeName = typeName.Substring(lastDot + 1);
        }
        // Remove generic arity if present (e.g., "List`1" -> "List")
        var genericMarker = typeName.IndexOf('`');
        if (genericMarker >= 0)
        {
            typeName = typeName.Substring(0, genericMarker);
        }
        return typeName;
    }

    /// <summary>
    /// Gets the exception type chain for wrapped exceptions.
    /// Example: "HttpRequestException->SocketException"
    /// </summary>
    private static string? GetExceptionChain(Exception ex)
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
}
