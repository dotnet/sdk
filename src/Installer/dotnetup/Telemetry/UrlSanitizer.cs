// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

/// <summary>
/// Sanitizes URLs for telemetry to prevent PII leakage.
/// Only known safe domains are reported; unknown domains are replaced with "unknown".
/// </summary>
public static class UrlSanitizer
{
    /// <summary>
    /// Known .NET download domains that are safe to report in telemetry.
    /// Unknown domains are reported as "unknown" to prevent PII leakage
    /// from custom/private mirrors.
    /// </summary>
    /// <remarks>
    /// See: https://github.com/dotnet/vscode-dotnet-runtime/blob/main/vscode-dotnet-runtime-library/src/Acquisition/GlobalInstallerResolver.ts
    /// </remarks>
    public static readonly IReadOnlyList<string> KnownDownloadDomains =
    [
        "download.visualstudio.microsoft.com",
        "builds.dotnet.microsoft.com",
        "ci.dot.net",
        "dotnetcli.blob.core.windows.net",
        "dotnetcli.azureedge.net"  // Legacy CDN, may still be referenced
    ];

    /// <summary>
    /// Extracts and sanitizes the domain from a URL for telemetry purposes.
    /// Returns "unknown" for unrecognized domains to prevent PII leakage from custom mirrors.
    /// </summary>
    /// <param name="url">The URL to extract the domain from.</param>
    /// <returns>The domain if known, or "unknown" for unrecognized/private domains.</returns>
    public static string SanitizeDomain(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return "unknown";
        }

        try
        {
            var host = new Uri(url).Host;
            foreach (var knownDomain in KnownDownloadDomains)
            {
                if (host.Equals(knownDomain, StringComparison.OrdinalIgnoreCase))
                {
                    return host;
                }
            }
            return "unknown";
        }
        catch
        {
            return "unknown";
        }
    }
}
