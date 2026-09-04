// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

#if NET

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Microsoft.DotNet.HotReload;

/// <summary>
/// The complete HTTP surface of the browser tools provider.
///
/// The provider serves no JavaScript: the browser tools client and its configuration are part of the
/// application build output, which is what makes authenticating the provider with the build pinned
/// public key meaningful.
/// </summary>
internal sealed class BrowserToolsEndpointRouter(AbstractBrowserRefreshServer browserServer)
{
    public async Task HandleAsync(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";

        if (!HttpMethods.IsGet(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        var path = context.Request.Path.Value;

        if (path == BrowserToolsProtocol.RoutePrefix + BrowserToolsProtocol.ClearCachePath)
        {
            context.Response.Headers.Append("Clear-Site-Data", "\"cache\"");
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }

        if (path == BrowserToolsProtocol.RoutePrefix + BrowserToolsProtocol.ConnectPath)
        {
            await browserServer.AcceptBrowserConnectionAsync(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status404NotFound;
    }
}

#endif
