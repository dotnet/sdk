// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.NET.Build.Containers.Registry;

internal class DefaultRegistryAPI : IRegistryAPI
{

    public DefaultRegistryAPI(Uri baseUri, ILogger logger)
    {
        var isAmazonECRRegistry = baseUri.IsAmazonECRRegistry();
        Client = CreateClient(baseUri, isAmazonECRRegistry: isAmazonECRRegistry);
        Manifest = new DefaultManifestOperations(Client);
        Blob = new DefaultBlobOperations(Client, logger);
    }

    public IBlobOperations Blob { get; }
    public IManifestOperations Manifest { get; }
    private HttpClient Client { get; }
    private HttpClient CreateClient(Uri baseUri, bool isAmazonECRRegistry = false)
    {
        HttpMessageHandler clientHandler = new AuthHandshakeMessageHandler(new SocketsHttpHandler()
        {
            PooledConnectionLifetime = TimeSpan.FromMilliseconds(10 /* total guess */),
            // disabling cookies prevents CSRF tokens from being sent - some servers send these and
            // can't handle them being sent back - specifically, Harbor does this.
            // golang client libraries disable cookies as well for this reason.
            UseCookies = false
        });
        if (isAmazonECRRegistry)
        {
            clientHandler = new AmazonECRMessageHandler(clientHandler);
        }

        HttpClient client = new(clientHandler);
        client.BaseAddress = baseUri;
        client.DefaultRequestHeaders.Add("User-Agent", $".NET Container Library v{Constants.Version}");

        return client;
    }
}
