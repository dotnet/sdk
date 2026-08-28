// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.Watch.BrowserRefresh;

public sealed class BrowserRefreshMiddleware
{
    private static readonly MediaTypeHeaderValue s_textHtmlMediaType = new("text/html");
    private static readonly MediaTypeHeaderValue s_applicationJsonMediaType = new("application/json");
    private readonly RequestDelegate _next;
    private readonly ILogger<BrowserRefreshMiddleware> _logger;
    private string? _dotnetModifiableAssemblies = GetNonEmptyEnvironmentVariableValue("DOTNET_MODIFIABLE_ASSEMBLIES");
    private string? _aspnetcoreBrowserTools = GetNonEmptyEnvironmentVariableValue("__ASPNETCORE_BROWSER_TOOLS");

    public BrowserRefreshMiddleware(RequestDelegate next, ILogger<BrowserRefreshMiddleware> logger)
    {
        _next = next;
        _logger = logger;

        Log.MiddlewareLoaded(_logger, _dotnetModifiableAssemblies, _aspnetcoreBrowserTools);
    }

    private static string? GetNonEmptyEnvironmentVariableValue(string name)
        => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : null;

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsWebAssemblyBootRequest(context))
        {
            AttachWebAssemblyHeaders(context);
            await _next(context);
        }
        else if (IsBrowserDocumentRequest(context))
        {
            // Use a custom StreamWrapper to rewrite output on Write/WriteAsync
            using var responseStreamWrapper = new ResponseStreamWrapper(context, _logger);
            var originalBodyFeature = context.Features.Get<IHttpResponseBodyFeature>();
            context.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(responseStreamWrapper));

            try
            {
                await _next(context);

                // We complete the wrapper stream to ensure that any intermediate buffers
                // get fully flushed to the response stream. This is also required to
                // reliably determine whether script injection was performed.
                await responseStreamWrapper.CompleteAsync();
            }
            finally
            {
                context.Features.Set(originalBodyFeature);
            }

            if (responseStreamWrapper.IsHtmlResponse)
            {
                if (responseStreamWrapper.ScriptInjectionPerformed)
                {
                    Log.BrowserConfiguredForRefreshes(_logger);
                }
                else if (context.Response.Headers.TryGetValue(HeaderNames.ContentEncoding, out var contentEncodings))
                {
                    Log.ResponseCompressionDetected(_logger, contentEncodings);
                }
                else
                {
                    Log.FailedToConfiguredForRefreshes(_logger);
                }
            }
        }
        else
        {
            await _next(context);
        }
    }

    private void AttachWebAssemblyHeaders(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey("DOTNET-MODIFIABLE-ASSEMBLIES"))
            {
                if (_dotnetModifiableAssemblies != null)
                {
                    context.Response.Headers.Append("DOTNET-MODIFIABLE-ASSEMBLIES", _dotnetModifiableAssemblies);
                }
                else
                {
                    Log.ModifiableAssembliesNotSet(_logger);
                }
            }
            else
            {
                Log.ModifiableAssembliesHeaderAlreadySet(_logger);
            }

            if (!context.Response.Headers.ContainsKey("ASPNETCORE-BROWSER-TOOLS"))
            {
                if (_aspnetcoreBrowserTools != null)
                {
                    context.Response.Headers.Append("ASPNETCORE-BROWSER-TOOLS", _aspnetcoreBrowserTools);
                }
                else
                {
                    Log.BrowserToolsNotSet(_logger);
                }
            }
            else
            {
                Log.BrowserToolsHeaderAlreadySet(_logger);
            }

            return Task.CompletedTask;
        });
    }

    internal static bool IsWebAssemblyBootRequest(HttpContext context)
    {
        var request = context.Request;
        if (!HttpMethods.IsGet(request.Method))
        {
            return false;
        }

        if (request.Headers.TryGetValue("Sec-Fetch-Dest", out var values) &&
            !StringValues.IsNullOrEmpty(values) &&
            !string.Equals(values[0], "empty", StringComparison.OrdinalIgnoreCase))
        {
            // See https://github.com/dotnet/aspnetcore/issues/37326.
            // Only inject scripts that are destined for a browser page.
            return false;
        }

        if (!request.Path.HasValue ||
            !string.Equals(Path.GetFileName(request.Path.Value), "blazor.boot.json", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var typedHeaders = request.GetTypedHeaders();
        if (typedHeaders.Accept is not IList<MediaTypeHeaderValue> acceptHeaders)
        {
            return false;
        }

        for (var i = 0; i < acceptHeaders.Count; i++)
        {
            if (acceptHeaders[i].MatchesAllTypes || acceptHeaders[i].IsSubsetOf(s_applicationJsonMediaType))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsBrowserDocumentRequest(HttpContext context)
    {
        var request = context.Request;
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsPost(request.Method))
        {
            return false;
        }

        if (request.Headers.TryGetValue("Sec-Fetch-Dest", out var values) &&
            !StringValues.IsNullOrEmpty(values) &&
            !string.Equals(values[0], "document", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(values[0], "frame", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(values[0], "iframe", StringComparison.OrdinalIgnoreCase) &&
            !IsProgressivelyEnhancedNavigation(context.Request))
        {
            // See https://github.com/dotnet/aspnetcore/issues/37326.
            // Only inject scripts that are destined for a browser page.
            return false;
        }

        var typedHeaders = request.GetTypedHeaders();
        if (typedHeaders.Accept is not IList<MediaTypeHeaderValue> acceptHeaders)
        {
            return false;
        }

        for (var i = 0; i < acceptHeaders.Count; i++)
        {
            if (acceptHeaders[i].IsSubsetOf(s_textHtmlMediaType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsProgressivelyEnhancedNavigation(HttpRequest request)
    {
        // This is an exact copy from https://github.com/dotnet/aspnetcore/blob/bb2d778dc66aa998ea8e26db0e98e7e01423ff78/src/Components/Endpoints/src/Rendering/EndpointHtmlRenderer.Streaming.cs#L327-L332
        // For enhanced nav, the Blazor JS code controls the "accept" header precisely, so we can be very specific about the format
        var accept = request.Headers.Accept;
        return accept.Count == 1 && string.Equals(accept[0]!, "text/html; blazor-enhanced-nav=on", StringComparison.Ordinal);
    }

    internal void Test_SetEnvironment(string dotnetModifiableAssemblies, string aspnetcoreBrowserTools)
    {
        _dotnetModifiableAssemblies = dotnetModifiableAssemblies;
        _aspnetcoreBrowserTools = aspnetcoreBrowserTools;
    }

    internal static class Log
    {
        private const string Symbol = "🔃";

        private static readonly Action<ILogger, string?, string?, Exception?> _middlewareLoaded = LoggerMessage.Define<string?, string?>(
            LogLevel.Debug,
            new EventId(0, "MiddlewareLoaded"),
            $"{Symbol} Middleware loaded: DOTNET_MODIFIABLE_ASSEMBLIES={{ModifiableAssemblies}}, __ASPNETCORE_BROWSER_TOOLS={{BrowserTools}}");

        private static readonly Action<ILogger, Exception?> _setupResponseForBrowserRefresh = LoggerMessage.Define(
            LogLevel.Debug,
            new EventId(1, "SetUpResponseForBrowserRefresh"),
            $"{Symbol} Response markup is scheduled to include browser refresh script injection.");

        private static readonly Action<ILogger, Exception?> _browserConfiguredForRefreshes = LoggerMessage.Define(
            LogLevel.Debug,
            new EventId(2, "BrowserConfiguredForRefreshes"),
            $"{Symbol} Response markup was updated to include browser refresh script injection.");

        private static readonly Action<ILogger, Exception?> _failedToConfigureForRefreshes = LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(3, "FailedToConfiguredForRefreshes"),
            $"{Symbol} Unable to configure browser refresh script injection on the response. " +
            $"Consider manually adding '{ScriptInjectingStream.InjectedScript}' to the body of the page.");

        private static readonly Action<ILogger, StringValues, Exception?> _responseCompressionDetected = LoggerMessage.Define<StringValues>(
            LogLevel.Warning,
            new EventId(4, "ResponseCompressionDetected"),
            $"{Symbol} Unable to configure browser refresh script injection on the response. " +
            $"This may have been caused by the response's {HeaderNames.ContentEncoding}: '{{encoding}}'. " +
            "Consider disabling response compression.");

        private static readonly Action<ILogger, int, string?, Exception?> _scriptInjectionSkipped = LoggerMessage.Define<int, string?>(
            LogLevel.Debug,
            new EventId(6, "ScriptInjectionSkipped"),
            $"{Symbol} Browser refresh script injection skipped. Status code: {{StatusCode}}, Content type: {{ContentType}}");

        private static readonly Action<ILogger, Exception?> _modifiableAssembliesNotSet = LoggerMessage.Define(
            LogLevel.Debug,
            new EventId(7, "ModifiableAssembliesNotSet"),
            $"{Symbol} DOTNET_MODIFIABLE_ASSEMBLIES environment variable is not set, likely because hot reload is not enabled. The browser refresh feature may not work as expected.");

        private static readonly Action<ILogger, Exception?> _modifiableAssembliesHeaderAlreadySet = LoggerMessage.Define(
            LogLevel.Debug,
            new EventId(8, "ModifiableAssembliesHeaderAlreadySet"),
            $"{Symbol} DOTNET-MODIFIABLE-ASSEMBLIES header is already set.");

        private static readonly Action<ILogger, Exception?> _browserToolsNotSet = LoggerMessage.Define(
            LogLevel.Debug,
            new EventId(9, "BrowserToolsNotSet"),
            $"{Symbol} __ASPNETCORE_BROWSER_TOOLS environment variable is not set. The browser refresh feature may not work as expected.");

        private static readonly Action<ILogger, Exception?> _browserToolsHeaderAlreadySet = LoggerMessage.Define(
            LogLevel.Debug,
            new EventId(10, "BrowserToolsHeaderAlreadySet"),
            $"{Symbol} ASPNETCORE-BROWSER-TOOLS header is already set.");

        public static void MiddlewareLoaded(ILogger logger, string? modifiableAssemblies, string? browserTools) => _middlewareLoaded(logger, modifiableAssemblies, browserTools, null);
        public static void SetupResponseForBrowserRefresh(ILogger logger) => _setupResponseForBrowserRefresh(logger, null);
        public static void BrowserConfiguredForRefreshes(ILogger logger) => _browserConfiguredForRefreshes(logger, null);
        public static void FailedToConfiguredForRefreshes(ILogger logger) => _failedToConfigureForRefreshes(logger, null);
        public static void ResponseCompressionDetected(ILogger logger, StringValues encoding) => _responseCompressionDetected(logger, encoding, null);
        public static void ScriptInjectionSkipped(ILogger logger, int statusCode, string? contentType) => _scriptInjectionSkipped(logger, statusCode, contentType, null);
        public static void ModifiableAssembliesNotSet(ILogger logger) => _modifiableAssembliesNotSet(logger, null);
        public static void ModifiableAssembliesHeaderAlreadySet(ILogger logger) => _modifiableAssembliesHeaderAlreadySet(logger, null);
        public static void BrowserToolsNotSet(ILogger logger) => _browserToolsNotSet(logger, null);
        public static void BrowserToolsHeaderAlreadySet(ILogger logger) => _browserToolsHeaderAlreadySet(logger, null);
    }
}
