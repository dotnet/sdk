// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;

namespace Microsoft.AspNetCore.Watch.BrowserRefresh;

internal static class BrowserToolsEnvironment
{
    public const string WebSocketEndpoint = "ASPNETCORE_AUTO_RELOAD_WS_ENDPOINT";
    public const string UseLegacyHtmlInjection = "ASPNETCORE_AUTO_RELOAD_USE_LEGACY_HTML_INJECTION";

    public static bool IsActive
        => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(WebSocketEndpoint));

    public static bool LegacyHtmlInjectionEnabled
        => string.Equals(
            Environment.GetEnvironmentVariable(UseLegacyHtmlInjection),
            bool.TrueString,
            StringComparison.OrdinalIgnoreCase);
}
