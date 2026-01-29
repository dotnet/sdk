// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Initializes the Microsoft.Deployment.DotNet.Releases library's HttpClient with a custom user-agent.
/// This differentiates library HTTP calls from dnup's direct HTTP calls for telemetry purposes.
/// </summary>
internal static class ReleaseLibraryInitializer
{
    private static bool _initialized = false;
    private static readonly object _lock = new();

    /// <summary>
    /// Configures the Microsoft.Deployment.DotNet.Releases library's static HttpClient with a custom user-agent.
    /// This method should be called once at application startup.
    /// </summary>
    public static void Initialize()
    {
        lock (_lock)
        {
            if (_initialized)
            {
                return;
            }

            try
            {
                // Use reflection to access the library's internal static HttpClient
                var utilsType = Type.GetType("Microsoft.Deployment.DotNet.Releases.Utils, Microsoft.Deployment.DotNet.Releases");
                if (utilsType == null)
                {
                    // Library not loaded or type not found - this is not fatal, just means we can't customize
                    _initialized = true;
                    return;
                }

                var httpClientField = utilsType.GetField("s_httpClient", BindingFlags.Static | BindingFlags.NonPublic);
                if (httpClientField == null)
                {
                    _initialized = true;
                    return;
                }

                var httpClient = httpClientField.GetValue(null) as HttpClient;
                if (httpClient != null)
                {
                    // Set user-agent to identify library calls, including version
                    var informationalVersion = typeof(ReleaseLibraryInitializer).Assembly
                        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                    string userAgent = informationalVersion == null 
                        ? "dotnetup-library" 
                        : $"dotnetup-library/{informationalVersion}";

                    httpClient.DefaultRequestHeaders.UserAgent.Clear();
                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
                }

                _initialized = true;
            }
            catch (Exception ex)
            {
                // If we can't set the user-agent, it's not critical - the library will still work
                // Just means we can't differentiate library calls from dnup calls
                // Log exception for diagnostic purposes
                System.Diagnostics.Debug.WriteLine($"ReleaseLibraryInitializer: Failed to set user-agent: {ex.Message}");
                _initialized = true;
            }
        }
    }
}
