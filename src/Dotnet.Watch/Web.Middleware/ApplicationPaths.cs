// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.Watch.BrowserRefresh;

internal static class ApplicationPaths
{
    /// <summary>
    /// The PathString all listening URLs must be registered in
    /// </summary>
    /// <value><c>/_framework/</c></value>
    public static PathString FrameworkRoot { get; } = "/_framework";

    public static PathString BrowserTools { get; } = FrameworkRoot + "/dotnet-browser-tools";

    public static PathString BrowserToolsBootstrapJS { get; } = BrowserTools + "/browser-tools-bootstrap.js";
}
