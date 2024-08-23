// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.Watch.BrowserRefresh
{
    public class BrowserRefreshMiddleware
    {
        private static readonly MediaTypeHeaderValue _textHtmlMediaType = new("text/html");
        private static readonly MediaTypeHeaderValue _applicationJsonMediaType = new("application/json");
        private readonly string? _dotnetModifiableAssemblies = GetNonEmptyEnvironmentVariableValue("DOTNET_MODIFIABLE_ASSEMBLIES");
        private readonly string? _aspnetcoreBrowserTools = GetNonEmptyEnvironmentVariableValue("__ASPNETCORE_BROWSER_TOOLS");

        private readonly RequestDelegate _next;
        private readonly ILogger _logger;

        private static string? GetNonEmptyEnvironmentVariableValue(string name)
            => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : null;

        public BrowserRefreshMiddleware(RequestDelegate next, ILogger<BrowserRefreshMiddleware> logger) =>
            (_next, _logger) = (next, logger);

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
                    if(_dotnetModifiableAssemblies != null)
                    {
                        context.Response.Headers.Add("DOTNET-MODIFIABLE-ASSEMBLIES", _dotnetModifiableAssemblies);
                    }
                    else
                    {
                        _logger.LogDebug("DOTNET_MODIFIABLE_ASSEMBLIES environment variable is not set, likely because hot reload is not enabled. The browser refresh feature may not work as expected.");
                    }
                }
                else
                {
                    _logger.LogDebug("DOTNET-MODIFIABLE-ASSEMBLIES header is already set.");
                }

                if (!context.Response.Headers.ContainsKey("ASPNETCORE-BROWSER-TOOLS"))
                {
                    if (_aspnetcoreBrowserTools != null)
                    {
                        context.Response.Headers.Add("ASPNETCORE-BROWSER-TOOLS", _aspnetcoreBrowserTools);
                    }
                    else
                    {
                        _logger.LogDebug("__ASPNETCORE_BROWSER_TOOLS environment variable is not set. The browser refresh feature may not work as expected.");
                    }
                }
                else
                {
                    _logger.LogDebug("ASPNETCORE-BROWSER-TOOLS header is already set.");
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
                if (acceptHeaders[i].MatchesAllTypes || acceptHeaders[i].IsSubsetOf(_applicationJsonMediaType))
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
                !string.Equals(values[0], "document", StringComparison.OrdinalIgnoreCase))
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
                if (acceptHeaders[i].IsSubsetOf(_textHtmlMediaType))
                {
                    return true;
                }
            }

            return false;
        }

        internal static class Log
        {
            private static readonly Action<ILogger, Exception?> _setupResponseForBrowserRefresh = LoggerMessage.Define(
                LogLevel.Debug,
                new EventId(1, "SetUpResponseForBrowserRefresh"),
                "Response markup is scheduled to include browser refresh script injection.");

            private static readonly Action<ILogger, Exception?> _browserConfiguredForRefreshes = LoggerMessage.Define(
                LogLevel.Debug,
                new EventId(2, "BrowserConfiguredForRefreshes"),
                "Response markup was updated to include browser refresh script injection.");

            private static readonly Action<ILogger, Exception?> _failedToConfigureForRefreshes = LoggerMessage.Define(
                LogLevel.Warning,
                new EventId(3, "FailedToConfiguredForRefreshes"),
                "Unable to configure browser refresh script injection on the response. " +
                $"Consider manually adding '{WebSocketScriptInjection.InjectedScript}' to the body of the page.");

            private static readonly Action<ILogger, StringValues, Exception?> _responseCompressionDetected = LoggerMessage.Define<StringValues>(
                LogLevel.Warning,
                new EventId(4, "ResponseCompressionDetected"),
                "Unable to configure browser refresh script injection on the response. " +
                $"This may have been caused by the response's {HeaderNames.ContentEncoding}: '{{encoding}}'. " +
                "Consider disabling response compression.");

            public static void SetupResponseForBrowserRefresh(ILogger logger) => _setupResponseForBrowserRefresh(logger, null);
            public static void BrowserConfiguredForRefreshes(ILogger logger) => _browserConfiguredForRefreshes(logger, null);
            public static void FailedToConfiguredForRefreshes(ILogger logger) => _failedToConfigureForRefreshes(logger, null);
            public static void ResponseCompressionDetected(ILogger logger, StringValues encoding) => _responseCompressionDetected(logger, encoding, null);
        }
    }
}
