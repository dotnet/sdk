// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

#if NET

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Microsoft.DotNet.HotReload;

internal sealed class BrowserToolsEndpointRouter(
    Guid sessionId,
    string publicKey,
    IBrowserToolsUpdateStore updateStore,
    AbstractBrowserRefreshServer browserServer)
{
    private static readonly ReadOnlyMemory<byte> s_clientModule = ReadClientModule();
    private static readonly ReadOnlyMemory<byte> s_bootstrapModule = Encoding.UTF8.GetBytes(
        "import { startBrowserTools } from './browser-tools.js';\nstartBrowserTools();\n");

    public async Task HandleAsync(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";

        if (!HttpMethods.IsGet(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        var path = context.Request.Path.Value;
        if (path == BrowserToolsProtocol.RoutePrefix + BrowserToolsProtocol.SessionPath)
        {
            await WriteJsonAsync(
                context,
                new BrowserToolsSessionDescriptor(
                    BrowserToolsProtocol.Version,
                    sessionId,
                    updateStore.GenerationId,
                    publicKey));
            return;
        }

        if (path == BrowserToolsProtocol.RoutePrefix + BrowserToolsProtocol.ClearCachePath)
        {
            context.Response.Headers.Append("Clear-Site-Data", "\"cache\"");
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }

        if (path == BrowserToolsProtocol.RoutePrefix + BrowserToolsProtocol.ClientModulePath)
        {
            await WriteJavaScriptAsync(context, s_clientModule);
            return;
        }

        if (path == BrowserToolsProtocol.RoutePrefix + BrowserToolsProtocol.BootstrapModulePath)
        {
            await WriteJavaScriptAsync(context, s_bootstrapModule);
            return;
        }

        if (path == BrowserToolsProtocol.RoutePrefix + BrowserToolsProtocol.ConnectPath)
        {
            await browserServer.AcceptBrowserConnectionAsync(context);
            return;
        }

        var updatesPrefix = BrowserToolsProtocol.RoutePrefix + BrowserToolsProtocol.UpdatesPath + "/";
        if (path?.StartsWith(updatesPrefix, StringComparison.Ordinal) == true &&
            path.EndsWith(".json", StringComparison.Ordinal) &&
            Guid.TryParse(path.AsSpan(updatesPrefix.Length, path.Length - updatesPrefix.Length - ".json".Length), out var generationId))
        {
            var replay = updateStore.GetReplay(generationId);
            if (replay.Status == BrowserToolsReplayStatus.GenerationMismatch)
            {
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                return;
            }

            await WriteJsonAsync(context, replay.Updates);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status404NotFound;
    }

    private static async Task WriteJsonAsync<TValue>(HttpContext context, TValue value)
    {
        var content = AbstractBrowserRefreshServer.SerializeJson(value);
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/json";
        context.Response.ContentLength = content.Length;
        await context.Response.Body.WriteAsync(content);
    }

    private static async Task WriteJavaScriptAsync(HttpContext context, ReadOnlyMemory<byte> content)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/javascript; charset=utf-8";
        context.Response.ContentLength = content.Length;
        await context.Response.Body.WriteAsync(content);
    }

    private static ReadOnlyMemory<byte> ReadClientModule()
    {
        using var stream = typeof(BrowserToolsEndpointRouter).Assembly.GetManifestResourceStream("Microsoft.DotNet.HotReload.BrowserTools.js")
            ?? throw new InvalidOperationException("Browser tools client module resource is missing.");
        using var content = new MemoryStream();
        stream.CopyTo(content);
        return content.ToArray();
    }

    private sealed record BrowserToolsSessionDescriptor(
        int ProtocolVersion,
        Guid SessionId,
        Guid GenerationId,
        string PublicKey);
}

#endif
