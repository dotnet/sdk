// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework
{
    public static class NuGetTransientErrorDetector
    {
        /// <summary>
        /// Error substrings that indicate a transient NuGet failure when "NuGet.targets" is also present in the output.
        /// </summary>
        private static readonly List<string> _errorSubstrings = new()
        {
            "A connection attempt failed because the connected party did not properly respond after a period of time",
            "Response status code does not indicate success: 5", // match any 5xx error
            "Response status code does not indicate success: 429", // 429 Too Many Requests
            "An error occurred while sending the request"
        };

        /// <summary>
        /// Error substrings that are unambiguously transient network failures regardless of context.
        /// These do not require "NuGet.targets" to be present (e.g. dotnet tool install failures).
        /// </summary>
        private static readonly List<string> _alwaysTransientSubstrings = new()
        {
            "Unable to read data from the transport connection",
            "The connection was closed unexpectedly"
        };

        public static bool IsTransientError(string? errorMessage)
        {
            if (errorMessage is null)
            {
                return false;
            }

            if (_alwaysTransientSubstrings.Any(errorMessage.Contains))
            {
                return true;
            }

            return errorMessage.Contains("NuGet.targets") && _errorSubstrings.Any(errorMessage.Contains);
        }
    }
}
