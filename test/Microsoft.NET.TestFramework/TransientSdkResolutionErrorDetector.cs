// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework
{
    /// <summary>
    /// Detects transient failures to resolve an in-box .NET SDK (for example
    /// <c>Microsoft.NET.Sdk.StaticWebAssets</c> imported by the Razor SDK).
    /// </summary>
    /// <remarks>
    /// Under heavy parallel I/O (many concurrent <c>dotnet build</c>/<c>dotnet new</c> invocations sharing a single
    /// SDK-under-test) the MSBuild default SDK resolver intermittently fails to probe the
    /// <c>Sdks/&lt;name&gt;/Sdk</c> folder, producing an <c>MSB4236</c> "could not be found" error even though the
    /// SDK is present on disk. These failures are not deterministic and succeed when retried, so they are treated as
    /// transient for the purposes of <see cref="Commands.TestCommand"/> retry logic. A genuinely missing SDK still
    /// fails after the retries are exhausted.
    /// </remarks>
    public static class TransientSdkResolutionErrorDetector
    {
        public static bool IsTransientError(string? errorMessage)
        {
            if (errorMessage is null)
            {
                return false;
            }

            return IsWorkloadResolverDeferralFlake(errorMessage)
                || IsDefaultResolverDirectoryFlake(errorMessage);
        }

        /// <summary>
        /// Pattern 1: The workload resolver defers (returns null) under parallel I/O and then the
        /// default resolver also fails to find the SDK directory, producing MSB4236.
        /// </summary>
        private static bool IsWorkloadResolverDeferralFlake(string errorMessage)
        {
            // The combination below is specific to the transient in-box SDK resolution flake:
            //   - MSB4236 is raised for an SDK that "could not be found",
            //   - for an in-box SDK in the Microsoft.NET.Sdk family, and
            //   - after the .NET SDK workload resolver (Microsoft.DotNet.MSBuildWorkloadSdkResolver) deferred
            //     resolution by returning null.
            // Keying on the specific resolver name (rather than a bare "returned null" substring) avoids
            // accidental retries when some other resolver emits a similar message, and requiring all three
            // avoids retrying legitimate failures such as a NuGet-versioned SDK that is intentionally absent.
            return errorMessage.Contains("MSB4236")
                && errorMessage.Contains("Microsoft.NET.Sdk")
                && errorMessage.Contains("Microsoft.DotNet.MSBuildWorkloadSdkResolver")
                && errorMessage.Contains("returned null");
        }

        /// <summary>
        /// Pattern 2: The default SDK resolver fails to probe the Sdks directory under heavy parallel
        /// I/O, reporting MSB4276 with "did not exist" even though the directory is present on disk.
        /// This is a race condition in filesystem enumeration under load.
        /// </summary>
        private static bool IsDefaultResolverDirectoryFlake(string errorMessage)
        {
            return errorMessage.Contains("MSB4276")
                && errorMessage.Contains("Microsoft.NET.Sdk")
                && errorMessage.Contains("did not exist");
        }
    }
}
