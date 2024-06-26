// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// A delegating handler that falls back from https to http for a specific hostname.
/// </summary>
internal sealed partial class FallbackToHttpMessageHandler : DelegatingHandler
{
    private readonly string _host;
    private readonly int _port;
    private readonly ILogger _logger;
    private bool _fallbackToHttp;

    public FallbackToHttpMessageHandler(string host, int port, HttpMessageHandler innerHandler, ILogger logger) : base(innerHandler)
    {
        _host = host;
        _port = port;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is null)
        {
            throw new ArgumentException(Resource.GetString(nameof(Strings.NoRequestUriSpecified)), nameof(request));
        }

        bool canFallback = request.RequestUri.Host == _host && request.RequestUri.Port == _port && request.RequestUri.Scheme == "https";
        do
        {
            try
            {
                if (canFallback && _fallbackToHttp)
                {
                    FallbackToHttp(request);
                    canFallback = false;
                }

                return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException re) when (canFallback && ShouldAttemptFallbackToHttp(re))
            {
                string uri = request.RequestUri.ToString();
                try
                {
                    // Try falling back.
                    _logger.LogTrace("Attempt to fall back to http for {uri}.", uri);
                    FallbackToHttp(request);
                    HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

                    // Fall back was successful. Use http for all new requests.
                    _logger.LogTrace("Fall back to http for {uri} was successful.", uri);
                    _fallbackToHttp = true;

                    return response;
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex, "Fall back to http for {uri} failed with message \"{message}\".", uri, ex.Message);
                }

                // Falling back didn't work, throw original exception.
                throw;
            }
        } while (true);
    }

    internal static bool ShouldAttemptFallbackToHttp(HttpRequestException exception)
    {
        return exception.HttpRequestError == HttpRequestError.SecureConnectionError;
    }

    private static void FallbackToHttp(HttpRequestMessage request)
    {
        var uriBuilder = new UriBuilder(request.RequestUri!);
        uriBuilder.Scheme = "http";
        request.RequestUri = uriBuilder.Uri;
    }
}
