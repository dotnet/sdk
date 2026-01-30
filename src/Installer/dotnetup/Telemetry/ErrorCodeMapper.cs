// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using Microsoft.Dotnet.Installation;

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
/// <param name="SourceLocation">Method name from our code where error occurred (no file paths).</param>
/// <param name="ExceptionChain">Chain of exception types for wrapped exceptions.</param>
public sealed record ExceptionErrorInfo(
    string ErrorType,
    ErrorCategory Category = ErrorCategory.Product,
    int? StatusCode = null,
    int? HResult = null,
    string? Details = null,
    string? SourceLocation = null,
    string? ExceptionChain = null);

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

        // Get common enrichment data
        var sourceLocation = GetSafeSourceLocation(ex);
        var exceptionChain = GetExceptionChain(ex);

        return ex switch
        {
            // DotnetInstallException has specific error codes - categorize by error code
            DotnetInstallException installEx => new ExceptionErrorInfo(
                installEx.ErrorCode.ToString(),
                Category: GetInstallErrorCategory(installEx.ErrorCode),
                Details: installEx.Version,
                SourceLocation: sourceLocation,
                ExceptionChain: exceptionChain),

            // HTTP errors: 4xx client errors are often user issues, 5xx are product/server issues
            HttpRequestException httpEx => new ExceptionErrorInfo(
                httpEx.StatusCode.HasValue ? $"Http{(int)httpEx.StatusCode}" : "HttpRequestException",
                Category: GetHttpErrorCategory(httpEx.StatusCode),
                StatusCode: (int?)httpEx.StatusCode,
                SourceLocation: sourceLocation,
                ExceptionChain: exceptionChain),

            // FileNotFoundException before IOException (it derives from IOException)
            // Could be user error (wrong path) or product error (our code referenced wrong file)
            // Default to product since we should handle missing files gracefully
            FileNotFoundException fnfEx => new ExceptionErrorInfo(
                "FileNotFound",
                Category: ErrorCategory.Product,
                HResult: fnfEx.HResult,
                Details: fnfEx.FileName is not null ? "file_specified" : null,
                SourceLocation: sourceLocation,
                ExceptionChain: exceptionChain),

            // Permission denied - user environment issue (needs elevation or different permissions)
            UnauthorizedAccessException => new ExceptionErrorInfo(
                "PermissionDenied",
                Category: ErrorCategory.User,
                SourceLocation: sourceLocation,
                ExceptionChain: exceptionChain),

            // Directory not found - could be user specified bad path
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

            // Invalid argument - user provided bad input
            ArgumentException argEx => new ExceptionErrorInfo(
                "InvalidArgument",
                Category: ErrorCategory.User,
                Details: argEx.ParamName,
                SourceLocation: sourceLocation,
                ExceptionChain: exceptionChain),

            // Invalid operation - usually a bug in our code
            InvalidOperationException => new ExceptionErrorInfo(
                "InvalidOperation",
                Category: ErrorCategory.Product,
                SourceLocation: sourceLocation,
                ExceptionChain: exceptionChain),

            // Not supported - could be user trying unsupported scenario
            NotSupportedException => new ExceptionErrorInfo(
                "NotSupported",
                Category: ErrorCategory.User,
                SourceLocation: sourceLocation,
                ExceptionChain: exceptionChain),

            // Timeout - network/environment issue outside our control
            TimeoutException => new ExceptionErrorInfo(
                "Timeout",
                Category: ErrorCategory.User,
                SourceLocation: sourceLocation,
                ExceptionChain: exceptionChain),

            // Unknown exceptions default to product (fail-safe - we should handle known cases)
            _ => new ExceptionErrorInfo(
                ex.GetType().Name,
                Category: ErrorCategory.Product,
                SourceLocation: sourceLocation,
                ExceptionChain: exceptionChain)
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
            DotnetInstallErrorCode.NoMatchingFile => ErrorCategory.Product,    // Our manifest/logic issue
            DotnetInstallErrorCode.DownloadFailed => ErrorCategory.Product,    // Server or download logic issue
            DotnetInstallErrorCode.HashMismatch => ErrorCategory.Product,      // Corrupted download or server issue
            DotnetInstallErrorCode.ExtractionFailed => ErrorCategory.Product,  // Our extraction code issue
            DotnetInstallErrorCode.Unknown => ErrorCategory.Product,           // Unknown = assume product issue

            _ => ErrorCategory.Product  // Default to product for new codes
        };
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

    private static ExceptionErrorInfo MapIOException(IOException ioEx, string? sourceLocation, string? exceptionChain)
    {
        string errorType;
        string? details;
        ErrorCategory category;

        // On Windows, use Win32Exception to get the readable error message
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && ioEx.HResult != 0)
        {
            // Extract the Win32 error code from HResult (lower 16 bits)
            var win32ErrorCode = ioEx.HResult & 0xFFFF;
            var win32Ex = new Win32Exception(win32ErrorCode);
            details = win32Ex.Message;

            // Derive a short error type from the HResult
            errorType = GetWindowsErrorType(ioEx.HResult);
            category = GetIOErrorCategory(errorType);
        }
        else
        {
            // On non-Windows or if no HResult, use our mapping
            (errorType, details) = GetErrorTypeFromHResult(ioEx.HResult);
            category = GetIOErrorCategory(errorType);
        }

        return new ExceptionErrorInfo(
            errorType,
            Category: category,
            HResult: ioEx.HResult,
            Details: details,
            SourceLocation: sourceLocation,
            ExceptionChain: exceptionChain);
    }

    /// <summary>
    /// Gets the error category for an IO error type.
    /// </summary>
    private static ErrorCategory GetIOErrorCategory(string errorType)
    {
        return errorType switch
        {
            // User environment issues - we can't control these
            "DiskFull" => ErrorCategory.User,
            "PermissionDenied" => ErrorCategory.User,
            "InvalidPath" => ErrorCategory.User,            // User specified invalid path
            "PathNotFound" => ErrorCategory.User,           // User's directory doesn't exist
            "NetworkPathNotFound" => ErrorCategory.User,    // Network issue
            "NetworkNameDeleted" => ErrorCategory.User,     // Network issue
            "DeviceFailure" => ErrorCategory.User,          // Hardware issue

            // Product issues - we should handle these gracefully
            "SharingViolation" => ErrorCategory.Product,    // Could be our mutex/lock issue
            "LockViolation" => ErrorCategory.Product,       // Could be our mutex/lock issue
            "PathTooLong" => ErrorCategory.Product,         // We control the install path
            "SemaphoreTimeout" => ErrorCategory.Product,    // Could be our concurrency issue
            "AlreadyExists" => ErrorCategory.Product,       // We should handle existing files gracefully
            "FileExists" => ErrorCategory.Product,          // We should handle existing files gracefully
            "FileNotFound" => ErrorCategory.Product,        // Our code referenced missing file
            "GeneralFailure" => ErrorCategory.Product,      // Unknown IO error
            "InvalidParameter" => ErrorCategory.Product,    // Our code passed bad params
            "IOException" => ErrorCategory.Product,         // Generic IO - assume product

            _ => ErrorCategory.Product                      // Unknown - assume product
        };
    }

    /// <summary>
    /// Gets a short error type name from a Windows HResult.
    /// </summary>
    private static string GetWindowsErrorType(int hResult)
    {
        return hResult switch
        {
            unchecked((int)0x80070070) or unchecked((int)0x80070027) => "DiskFull",
            unchecked((int)0x80070005) => "PermissionDenied",
            unchecked((int)0x80070020) => "SharingViolation",
            unchecked((int)0x80070021) => "LockViolation",
            unchecked((int)0x800700CE) => "PathTooLong",
            unchecked((int)0x8007007B) => "InvalidPath",
            unchecked((int)0x80070003) => "PathNotFound",
            unchecked((int)0x80070002) => "FileNotFound",
            unchecked((int)0x800700B7) => "AlreadyExists",
            unchecked((int)0x80070050) => "FileExists",
            unchecked((int)0x80070035) => "NetworkPathNotFound",
            unchecked((int)0x80070033) => "NetworkNameDeleted",
            unchecked((int)0x80004005) => "GeneralFailure",
            unchecked((int)0x8007001F) => "DeviceFailure",
            unchecked((int)0x80070057) => "InvalidParameter",
            unchecked((int)0x80070079) => "SemaphoreTimeout",
            _ => "IOException"
        };
    }

    /// <summary>
    /// Gets error type and details from HResult for non-Windows platforms.
    /// </summary>
    private static (string errorType, string? details) GetErrorTypeFromHResult(int hResult)
    {
        return hResult switch
        {
            // Disk/storage errors
            unchecked((int)0x80070070) => ("DiskFull", "ERROR_DISK_FULL"),
            unchecked((int)0x80070027) => ("DiskFull", "ERROR_HANDLE_DISK_FULL"),
            unchecked((int)0x80070079) => ("SemaphoreTimeout", "ERROR_SEM_TIMEOUT"),

            // Permission errors
            unchecked((int)0x80070005) => ("PermissionDenied", "ERROR_ACCESS_DENIED"),
            unchecked((int)0x80070020) => ("SharingViolation", "ERROR_SHARING_VIOLATION"),
            unchecked((int)0x80070021) => ("LockViolation", "ERROR_LOCK_VIOLATION"),

            // Path errors
            unchecked((int)0x800700CE) => ("PathTooLong", "ERROR_FILENAME_EXCED_RANGE"),
            unchecked((int)0x8007007B) => ("InvalidPath", "ERROR_INVALID_NAME"),
            unchecked((int)0x80070003) => ("PathNotFound", "ERROR_PATH_NOT_FOUND"),
            unchecked((int)0x80070002) => ("FileNotFound", "ERROR_FILE_NOT_FOUND"),

            // File/directory existence errors
            unchecked((int)0x800700B7) => ("AlreadyExists", "ERROR_ALREADY_EXISTS"),
            unchecked((int)0x80070050) => ("FileExists", "ERROR_FILE_EXISTS"),

            // Network errors
            unchecked((int)0x80070035) => ("NetworkPathNotFound", "ERROR_BAD_NETPATH"),
            unchecked((int)0x80070033) => ("NetworkNameDeleted", "ERROR_NETNAME_DELETED"),
            unchecked((int)0x80004005) => ("GeneralFailure", "E_FAIL"),

            // Device/hardware errors
            unchecked((int)0x8007001F) => ("DeviceFailure", "ERROR_GEN_FAILURE"),
            unchecked((int)0x80070057) => ("InvalidParameter", "ERROR_INVALID_PARAMETER"),

            // Default: include raw HResult for debugging
            _ => ("IOException", hResult != 0 ? $"0x{hResult:X8}" : null)
        };
    }

    /// <summary>
    /// Gets a safe source location from the stack trace - finds the first frame from our assemblies.
    /// This is typically the code in dotnetup that called into BCL/external code that threw.
    /// No file paths or line numbers that could contain user info.
    /// </summary>
    private static string? GetSafeSourceLocation(Exception ex)
    {
        try
        {
            var stackTrace = new StackTrace(ex, fNeedFileInfo: false);
            var frames = stackTrace.GetFrames();

            if (frames == null || frames.Length == 0)
            {
                return null;
            }

            string? throwSite = null;

            // Walk the stack from throw site upward, looking for the first frame in our code.
            // This finds the dotnetup code that called into BCL/external code that threw.
            foreach (var frame in frames)
            {
                var methodInfo = DiagnosticMethodInfo.Create(frame);
                if (methodInfo == null) continue;

                // DiagnosticMethodInfo provides DeclaringTypeName which includes the full type name
                var declaringType = methodInfo.DeclaringTypeName;
                if (string.IsNullOrEmpty(declaringType)) continue;

                // Capture the first frame as the throw site (fallback)
                if (throwSite == null)
                {
                    var throwTypeName = ExtractTypeName(declaringType);
                    throwSite = $"[BCL]{throwTypeName}.{methodInfo.Name}";
                }

                // Check if it's from our assemblies by looking at the namespace prefix
                if (IsOwnedNamespace(declaringType))
                {
                    // Extract just the type name (last part after the last dot, before any generic params)
                    var typeName = ExtractTypeName(declaringType);

                    // Return "TypeName.MethodName" - no paths, no line numbers
                    return $"{typeName}.{methodInfo.Name}";
                }
            }

            // If we didn't find our code, return the throw site as a fallback
            return throwSite;
        }
        catch
        {
            // Never fail telemetry due to stack trace parsing
            return null;
        }
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
