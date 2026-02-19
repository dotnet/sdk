// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Microsoft.Dotnet.Installation;

namespace Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

/// <summary>
/// Analyzes network-related exceptions to extract telemetry-safe diagnostic info
/// (HTTP status codes, socket error codes) without leaking PII.
/// </summary>
internal static class NetworkErrorAnalyzer
{
    /// <summary>
    /// Checks if a <see cref="DotnetInstallErrorCode"/> is related to network operations.
    /// </summary>
    internal static bool IsNetworkRelatedErrorCode(DotnetInstallErrorCode errorCode)
    {
        return errorCode is
            DotnetInstallErrorCode.ManifestFetchFailed or
            DotnetInstallErrorCode.DownloadFailed or
            DotnetInstallErrorCode.NetworkError;
    }

    /// <summary>
    /// Walks the exception chain to find HTTP and socket-level diagnostic info,
    /// then determines the error category accordingly.
    /// </summary>
    /// <returns>
    /// A tuple of (Category, HttpStatus, Details) with PII-safe diagnostic information.
    /// </returns>
    internal static (ErrorCategory Category, int? HttpStatus, string? Details) AnalyzeNetworkException(Exception inner)
    {
        // Walk the exception chain to find HttpRequestException or SocketException.
        // Look for the most specific info we can find.
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
            var category = ErrorCategoryClassifier.ClassifyHttpError(foundHttpEx.StatusCode);
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
}
