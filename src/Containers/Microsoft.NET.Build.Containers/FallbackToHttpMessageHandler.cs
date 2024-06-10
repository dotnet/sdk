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
    private bool _fallbackToHttp;

    public FallbackToHttpMessageHandler(string host, int port, HttpMessageHandler innerHandler, ILogger logger) : base(innerHandler)
    {
        _host = host;
        _port = port;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is null)
        {
            throw new ArgumentException(Resource.GetString(nameof(Strings.NoRequestUriSpecified)), nameof(request));
        }

        bool canFallback = request.RequestUri.Host == _host && request.RequestUri.Port == _port && request.RequestUri.Scheme == "https";
        bool canRetry = true;
        do
        {
            try
            {
                if (canFallback && _fallbackToHttp)
                {
                    var uriBuilder = new UriBuilder(request.RequestUri);
                    uriBuilder.Scheme = "http";
                    request.RequestUri = uriBuilder.Uri;
                    canRetry = false;
                }

                return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException re) when (canFallback && canRetry && re.HttpRequestError == HttpRequestError.SecureConnectionError)
            {
                _fallbackToHttp = true;
            }
        } while (true);
    }
}
