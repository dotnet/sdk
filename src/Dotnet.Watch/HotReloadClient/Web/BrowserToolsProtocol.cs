// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.DotNet.HotReload;

internal static class BrowserToolsProtocol
{
    public const int Version = 1;
    public const string RoutePrefix = "/_framework/dotnet-browser-tools";
    public const string SessionPath = "/session.json";
    public const string UpdatesPath = "/updates";
    public const string ClearCachePath = "/clear-cache";
    public const string ConnectPath = "/connect";
    public const string ClientModulePath = "/browser-tools.js";
    public const string BootstrapModulePath = "/browser-tools-bootstrap.js";
}
