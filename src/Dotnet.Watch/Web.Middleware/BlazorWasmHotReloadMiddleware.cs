// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.Watch.BrowserRefresh;

/// <summary>
/// A middleware that manages receiving and sending deltas from a BlazorWebAssembly app.
/// This assembly is shared between Visual Studio and dotnet-watch. By putting some of the complexity
/// in here, we can avoid duplicating work in watch and VS.
///
/// Mapped to <see cref="ApplicationPaths.BlazorHotReloadMiddleware"/>.
/// </summary>
internal sealed class BlazorWasmHotReloadMiddleware
{
    internal sealed class Update
    {
        public int Id { get; set; }
        public Delta[] Deltas { get; set; } = default!;
    }

    internal sealed class Delta
    {
        public string ModuleId { get; set; } = default!;
        public string MetadataDelta { get; set; } = default!;
        public string ILDelta { get; set; } = default!;
        public string PdbDelta { get; set; } = default!;
        public int[] UpdatedTypes { get; set; } = default!;
    }

    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly char[] s_urlSeparators = [';', ','];

    private readonly IReadOnlyList<BindingAddress> _allowedOrigins;
    private readonly ILogger<BlazorWasmHotReloadMiddleware> _logger;

    public BlazorWasmHotReloadMiddleware(RequestDelegate next, ILogger<BlazorWasmHotReloadMiddleware> logger, IConfiguration configuration)
    {
        _logger = logger;
        _allowedOrigins = ParseServerUrls(logger, configuration[WebHostDefaults.ServerUrlsKey]);
        logger.LogDebug($"Middleware loaded. Allowed origins: {string.Join(";", _allowedOrigins.Select(a => a.ToString()))}");
    }

    internal List<Update> Updates { get; } = [];

    public Task InvokeAsync(HttpContext context)
    {
        // Multiple instances of the BlazorWebAssembly app could be running (multiple tabs or multiple browsers).
        // We want to avoid serialize reads and writes between then
        lock (Updates)
        {
            if (HttpMethods.IsGet(context.Request.Method))
            {
                return OnGet(context);
            }
            else if (HttpMethods.IsPost(context.Request.Method))
            {
                return OnPost(context);
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                return Task.CompletedTask;
            }
        }

        // Don't call next(). This middleware is terminal.
    }

    private async Task OnGet(HttpContext context)
    {
        if (Updates.Count == 0)
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }

        await JsonSerializer.SerializeAsync(context.Response.Body, Updates, s_jsonSerializerOptions);
    }

    private async Task OnPost(HttpContext context)
    {
        var origin = context.Request.Headers[HeaderNames.Origin];

        _logger.LogDebug($"WASM middleware request {context.Request.Method} from {origin}");

        if (!IsAllowedOrigin(origin, _allowedOrigins))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (context.Request.ContentType != "application/json")
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var update = await JsonSerializer.DeserializeAsync<Update>(context.Request.Body, s_jsonSerializerOptions);
        if (update == null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        // It's possible that multiple instances of the BlazorWasm are simultaneously executing and could be posting the same deltas
        // We'll use the sequence id to ensure that we're not recording duplicate entries. Replaying duplicated values would cause
        // ApplyDelta to fail.
        if (Updates is [] || Updates[^1].Id < update.Id)
        {
            Updates.Add(update);
        }
    }

    internal static IReadOnlyList<BindingAddress> ParseServerUrls(ILogger<BlazorWasmHotReloadMiddleware> logger, string? urls)
    {
        var result = new List<BindingAddress>();
        if (urls is null)
        {
            return result;
        }

        foreach (var value in urls.Split(s_urlSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            BindingAddress address;
            try
            {
                address = BindingAddress.Parse(value);
            }
            catch (FormatException)
            {
                // Ignore invalid URLs.
                logger.LogDebug($"Invalid binding address '{value}'");
                continue;
            }

            if (address.IsUnixPipe)
            {
                continue;
            }

            if (IsWildcardHost(address.Host))
            {
                address = BindingAddress.Parse($"{address.Scheme}://localhost:{address.Port}");
            }

            result.Add(address);
        }

        return result;
    }

    internal static bool IsAllowedOrigin(StringValues originHeader, IReadOnlyList<BindingAddress> allowedAddresses)
    {
        if (originHeader.Count != 1 ||
            !Uri.TryCreate(originHeader[0], UriKind.Absolute, out var requestOrigin) ||
            (requestOrigin.Scheme != Uri.UriSchemeHttp && requestOrigin.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        if (allowedAddresses is [])
        {
            return IsLoopbackHost(requestOrigin.Host);
        }

        foreach (var allowedAddress in allowedAddresses)
        {
            if (!string.Equals(allowedAddress.Scheme, requestOrigin.Scheme, StringComparison.OrdinalIgnoreCase) ||
                allowedAddress.Port != requestOrigin.Port)
            {
                continue;
            }

            if (string.Equals(allowedAddress.Host, requestOrigin.Host, StringComparison.OrdinalIgnoreCase) ||
                (IsLoopbackHost(allowedAddress.Host) && IsLoopbackHost(requestOrigin.Host)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWildcardHost(string host)
        => host is "*" or "+" or "0.0.0.0" or "[::]";

    private static bool IsLoopbackHost(string host)
        => host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
           host is "127.0.0.1" or "[::1]";
}
