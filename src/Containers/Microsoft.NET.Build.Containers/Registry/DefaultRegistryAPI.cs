// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace Microsoft.NET.Build.Containers;

internal class DefaultRegistryAPI : IRegistryAPI
{
    private readonly Uri _baseUri;
    private readonly HttpClient _client;
    private readonly ILogger _logger;

    // Empirical value - Unoptimized .NET application layers can be ~200MB
    // * .NET Runtime (~80MB)
    // * ASP.NET Runtime (~25MB)
    // * application and dependencies - variable, but _probably_ not more than the BCL?
    // Given a 200MB target and a 1Mb/s upload speed, we'd expect an upload speed of 27m:57s.
    // Making this a round 30 for convenience.
    private static TimeSpan LongRequestTimeout = TimeSpan.FromMinutes(30);

    internal DefaultRegistryAPI(string registryName, Uri baseUri, bool isInsecureRegistry, ILogger logger, RegistryMode mode)
    {
        _baseUri = baseUri;
        _logger = logger;
        _client = CreateClient(registryName, baseUri, logger, isInsecureRegistry, mode);
        Manifest = new DefaultManifestOperations(_baseUri, registryName, _client, _logger);
        Blob = new DefaultBlobOperations(_baseUri, registryName, _client, _logger);
    }

    public IBlobOperations Blob { get; }

    public IManifestOperations Manifest { get; }

    private static HttpClient CreateClient(string registryName, Uri baseUri, ILogger logger, bool isInsecureRegistry, RegistryMode mode)
    {
        HttpMessageHandler innerHandler = CreateHttpHandler(registryName, baseUri, isInsecureRegistry, logger);

        HttpMessageHandler clientHandler = new AuthHandshakeMessageHandler(registryName, innerHandler, logger, mode);

        if (baseUri.IsAmazonECRRegistry())
        {
            clientHandler = new AmazonECRMessageHandler(clientHandler);
        }

        HttpClient client = new(clientHandler)
        {
            Timeout = LongRequestTimeout
        };

        client.DefaultRequestHeaders.Add("User-Agent", $".NET Container Library v{Constants.Version}");

        return client;
    }

    private static HttpMessageHandler CreateHttpHandler(string registryName, Uri baseUri, bool allowInsecure, ILogger logger)
    {
        var socketsHttpHandler = new SocketsHttpHandler()
        {
            UseCookies = false,
            // the rest of the HTTP stack has an very long timeout (see below) but we should still have a reasonable timeout for the initial connection
            ConnectTimeout = TimeSpan.FromSeconds(30)
        };

        if (!allowInsecure)
        {
            return socketsHttpHandler;
        }

        socketsHttpHandler.SslOptions = new System.Net.Security.SslClientAuthenticationOptions()
        {
            RemoteCertificateValidationCallback = IgnoreCertificateErrorsForSpecificHost(baseUri.Host)
        };

        return new FallbackToHttpMessageHandler(registryName, baseUri.Host, baseUri.Port, socketsHttpHandler, logger);
    }

    private static RemoteCertificateValidationCallback IgnoreCertificateErrorsForSpecificHost(string host)
    {
        return (object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors) =>
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }

            // Ignore certificate errors for the hostname.
            if ((sender as SslStream)?.TargetHostName == host)
            {
                return true;
            }

            return false;
        };
    }
}
