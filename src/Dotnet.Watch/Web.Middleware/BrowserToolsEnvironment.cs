// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Net;

namespace Microsoft.AspNetCore.Watch.BrowserRefresh;

internal static class BrowserToolsEnvironment
{
    public const string WebSocketEndpoint = "ASPNETCORE_AUTO_RELOAD_WS_ENDPOINT";
    public const string UseLegacyHtmlInjection = "ASPNETCORE_AUTO_RELOAD_USE_LEGACY_HTML_INJECTION";
    public const string ProviderAddress = "ASPNETCORE_AUTO_RELOAD_PROVIDER_ADDRESS";

    public static bool IsLegacy
        => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(WebSocketEndpoint));

    public static Uri? GetProviderAddress()
    {
        var value = Environment.GetEnvironmentVariable(ProviderAddress);
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var address) ||
            address.Scheme != Uri.UriSchemeHttp ||
            !IsLoopbackHost(address))
        {
            throw new InvalidOperationException($"The configured browser tools provider address '{value}' is not a loopback HTTP address.");
        }

        return address;
    }

    public static bool LegacyHtmlInjectionEnabled
        => string.Equals(
            Environment.GetEnvironmentVariable(UseLegacyHtmlInjection),
            bool.TrueString,
            StringComparison.OrdinalIgnoreCase);

    private static bool IsLoopbackHost(Uri address)
        => string.Equals(address.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
           (IPAddress.TryParse(address.Host, out var ipAddress) && IPAddress.IsLoopback(ipAddress));
}
