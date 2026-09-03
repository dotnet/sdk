// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Net;

namespace Microsoft.AspNetCore.Watch.BrowserRefresh;

internal static class BrowserToolsEnvironment
{
    public const string ProviderAddress = "ASPNETCORE_AUTO_RELOAD_PROVIDER_ADDRESS";

    public static Uri GetProviderAddress()
    {
        var value = Environment.GetEnvironmentVariable(ProviderAddress);
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException($"The required browser tools provider address is not configured in '{ProviderAddress}'.");
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var address) ||
            address.Scheme != Uri.UriSchemeHttp ||
            !IsLoopbackHost(address))
        {
            throw new InvalidOperationException($"The configured browser tools provider address '{value}' is not a loopback HTTP address.");
        }

        return address;
    }

    private static bool IsLoopbackHost(Uri address)
        => string.Equals(address.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
           (IPAddress.TryParse(address.Host, out var ipAddress) && IPAddress.IsLoopback(ipAddress));
}
