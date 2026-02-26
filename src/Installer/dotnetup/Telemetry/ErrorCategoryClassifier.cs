// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Sockets;

namespace Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

/// <summary>
/// Classifies errors as <see cref="ErrorCategory.Product"/> or <see cref="ErrorCategory.User"/>.
/// </summary>
/// <remarks>
/// Product errors represent issues we can take action on (bugs, server problems, logic errors).
/// User errors represent issues outside our control (invalid input, disk full, network down).
/// This distinction drives quality metrics — product errors count against our success rate,
/// while user errors are tracked separately.
/// </remarks>
internal static class ErrorCategoryClassifier
{
    /// <summary>
    /// Classifies a <see cref="DotnetInstallErrorCode"/> as product or user error.
    /// </summary>
    internal static ErrorCategory ClassifyInstallError(DotnetInstallErrorCode errorCode)
    {
        return errorCode switch
        {
            // User errors - bad input or environment issues
            DotnetInstallErrorCode.VersionNotFound => ErrorCategory.User,
            DotnetInstallErrorCode.ReleaseNotFound => ErrorCategory.User,
            DotnetInstallErrorCode.InvalidChannel => ErrorCategory.User,
            DotnetInstallErrorCode.PermissionDenied => ErrorCategory.User,
            DotnetInstallErrorCode.DiskFull => ErrorCategory.User,
            DotnetInstallErrorCode.NetworkError => ErrorCategory.User,

            // Product errors - issues we can take action on
            DotnetInstallErrorCode.ExtractionFailed => ErrorCategory.Product,
            DotnetInstallErrorCode.NoMatchingReleaseFileForPlatform => ErrorCategory.Product,
            DotnetInstallErrorCode.DownloadFailed => ErrorCategory.Product,
            DotnetInstallErrorCode.HashMismatch => ErrorCategory.Product,
            DotnetInstallErrorCode.ManifestFetchFailed => ErrorCategory.Product,
            DotnetInstallErrorCode.ManifestParseFailed => ErrorCategory.Product,
            DotnetInstallErrorCode.ArchiveCorrupted => ErrorCategory.Product,
            DotnetInstallErrorCode.InstallationLocked => ErrorCategory.Product,
            DotnetInstallErrorCode.LocalManifestError => ErrorCategory.Product,
            DotnetInstallErrorCode.LocalManifestCorrupted => ErrorCategory.Product,
            DotnetInstallErrorCode.LocalManifestUserCorrupted => ErrorCategory.User,
            DotnetInstallErrorCode.Unknown => ErrorCategory.Product,

            _ => ErrorCategory.Product
        };
    }

    /// <summary>
    /// Classifies an HTTP status code as product or user error.
    /// </summary>
    internal static ErrorCategory ClassifyHttpError(HttpStatusCode? statusCode)
    {
        if (!statusCode.HasValue)
        {
            return ErrorCategory.User;
        }

        var code = (int)statusCode.Value;
        return code switch
        {
            >= 500 => ErrorCategory.Product,
            404 => ErrorCategory.User,
            403 => ErrorCategory.User,
            401 => ErrorCategory.User,
            408 => ErrorCategory.User,
            429 => ErrorCategory.User,
            _ => ErrorCategory.Product
        };
    }

    /// <summary>
    /// Checks if the error code is related to network operations.
    /// </summary>
    internal static bool IsNetworkRelatedErrorCode(DotnetInstallErrorCode errorCode)
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
    internal static bool IsIORelatedErrorCode(DotnetInstallErrorCode errorCode)
    {
        return errorCode is
            DotnetInstallErrorCode.ExtractionFailed or
            DotnetInstallErrorCode.LocalManifestError;
    }

    /// <summary>
    /// Analyzes a network-related inner exception to determine the category and extract details.
    /// </summary>
    internal static (ErrorCategory Category, int? HttpStatus, string? Details) AnalyzeNetworkException(Exception inner)
    {
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

        if (foundSocketEx is not null)
        {
            var socketErrorName = foundSocketEx.SocketErrorCode.ToString().ToLowerInvariant();
            return (ErrorCategory.User, null, $"socket_{socketErrorName}");
        }

        if (foundHttpEx is not null)
        {
            var category = ClassifyHttpError(foundHttpEx.StatusCode);
            var httpStatus = (int?)foundHttpEx.StatusCode;

            string? details = null;
            if (foundHttpEx.StatusCode.HasValue)
            {
                details = $"http_{(int)foundHttpEx.StatusCode}";
            }
            else if (foundHttpEx.HttpRequestError != HttpRequestError.Unknown)
            {
                details = $"request_error_{foundHttpEx.HttpRequestError.ToString().ToLowerInvariant()}";
            }

            return (category, httpStatus, details);
        }

        return (ErrorCategory.Product, null, "network_unknown");
    }

    /// <summary>
    /// Classifies an IO error by its HResult, returning the error type, category, and optional details
    /// in a single lookup. This avoids a two-step HResult→errorType→category pipeline that could
    /// get out of sync.
    /// </summary>
    internal static (string ErrorType, ErrorCategory Category, string? Details) ClassifyIOErrorByHResult(int hResult)
    {
        return hResult switch
        {
            // Disk/storage errors — user environment
            unchecked((int)0x80070070) => ("DiskFull", ErrorCategory.User, "ERROR_DISK_FULL"),
            unchecked((int)0x80070027) => ("DiskFull", ErrorCategory.User, "ERROR_HANDLE_DISK_FULL"),

            // Permission errors — user environment
            unchecked((int)0x80070005) => ("PermissionDenied", ErrorCategory.User, "ERROR_ACCESS_DENIED"),

            // Path errors — user environment
            unchecked((int)0x8007007B) => ("InvalidPath", ErrorCategory.User, "ERROR_INVALID_NAME"),
            unchecked((int)0x80070003) => ("PathNotFound", ErrorCategory.User, "ERROR_PATH_NOT_FOUND"),

            // Network errors — user environment
            unchecked((int)0x80070035) => ("NetworkPathNotFound", ErrorCategory.User, "ERROR_BAD_NETPATH"),
            unchecked((int)0x80070033) => ("NetworkNameDeleted", ErrorCategory.User, "ERROR_NETNAME_DELETED"),

            // Device/hardware errors — user environment
            unchecked((int)0x8007001F) => ("DeviceFailure", ErrorCategory.User, "ERROR_GEN_FAILURE"),

            // Sharing/lock violations — product issues (our mutex/lock logic)
            unchecked((int)0x80070020) => ("SharingViolation", ErrorCategory.Product, "ERROR_SHARING_VIOLATION"),
            unchecked((int)0x80070021) => ("LockViolation", ErrorCategory.Product, "ERROR_LOCK_VIOLATION"),

            // Semaphore timeout — product issue (our concurrency logic)
            unchecked((int)0x80070079) => ("SemaphoreTimeout", ErrorCategory.Product, "ERROR_SEM_TIMEOUT"),

            // Path too long — product issue (we control install paths)
            unchecked((int)0x800700CE) => ("PathTooLong", ErrorCategory.Product, "ERROR_FILENAME_EXCED_RANGE"),

            // File existence — product issue (we should handle gracefully)
            unchecked((int)0x80070002) => ("FileNotFound", ErrorCategory.Product, "ERROR_FILE_NOT_FOUND"),
            unchecked((int)0x800700B7) => ("AlreadyExists", ErrorCategory.Product, "ERROR_ALREADY_EXISTS"),
            unchecked((int)0x80070050) => ("FileExists", ErrorCategory.Product, "ERROR_FILE_EXISTS"),

            // General failures — product issue
            unchecked((int)0x80004005) => ("GeneralFailure", ErrorCategory.Product, "E_FAIL"),
            unchecked((int)0x80070057) => ("InvalidParameter", ErrorCategory.Product, "ERROR_INVALID_PARAMETER"),

            // Unknown — include raw HResult, assume product
            _ => ("IOException", ErrorCategory.Product, hResult != 0 ? $"0x{hResult:X8}" : null)
        };
    }
}
