// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using OrasProject.Oras.Registry;
using OrasProject.Oras.Registry.Remote;
using OrasProject.Oras.Registry.Remote.Auth;

namespace Microsoft.NET.Build.Containers;

internal interface IRepositoryFactory
{
    IRepository Create(string repositoryName);
}

internal sealed class OrasRepositoryFactory : IRepositoryFactory
{
    private const int DefaultChunkSizeBytes = 64 * 1024;
    private static readonly TimeSpan s_longRequestTimeout = TimeSpan.FromMinutes(30);

    private readonly Uri _baseUri;
    private readonly IClient _client;
    private readonly RegistrySettings _settings;

    internal OrasRepositoryFactory(Uri baseUri, IClient client, RegistrySettings settings)
    {
        _baseUri = baseUri;
        _client = client;
        _settings = settings;
    }

    internal OrasRepositoryFactory(string registryName, Uri baseUri, RegistrySettings settings, ILogger logger, RegistryMode mode)
        : this(baseUri, CreateClient(registryName, baseUri, settings, logger, mode), settings)
    {
    }

    public IRepository Create(string repositoryName) => new Repository(new RepositoryOptions
    {
        BlobUploadChunkSize = _settings.ChunkedUploadSizeBytes ?? DefaultChunkSizeBytes,
        BlobUploadMode = _settings.ForceChunkedUpload ?
            BlobUploadMode.Chunked :
            BlobUploadMode.MonolithicWithChunkedFallback,
        Client = _client,
        Reference = Reference.Parse($"{_baseUri.Authority}/{repositoryName}"),
    });

    private static Client CreateClient(string registryName, Uri baseUri, RegistrySettings settings, ILogger logger, RegistryMode mode)
    {
        bool isInsecureRegistry = settings.IsInsecure;
        Client client = new(
            CreateHttpClient(registryName, baseUri, logger, isInsecureRegistry, allowAutoRedirect: true),
            CreateHttpClient(registryName, baseUri, logger, isInsecureRegistry, allowAutoRedirect: false),
            new OrasCredentialProvider(mode),
            accessTokenProvider: null,
            cache: null)
        {
            ClientId = "netsdkcontainers",
            RealmValidator = new OrasRealmValidator(registryName, isInsecureRegistry),
        };
        client.SetUserAgent($".NET Container Library v{Constants.Version}");
        return client;
    }

    private static HttpClient CreateHttpClient(string registryName, Uri baseUri, ILogger logger, bool isInsecureRegistry, bool allowAutoRedirect)
    {
        HttpMessageHandler clientHandler = CreateHttpHandler(registryName, baseUri, isInsecureRegistry, allowAutoRedirect, logger);

        if (baseUri.IsAmazonECRRegistry())
        {
            clientHandler = new AmazonECRMessageHandler(clientHandler);
        }

        return new HttpClient(clientHandler) { Timeout = s_longRequestTimeout };
    }

    private static HttpMessageHandler CreateHttpHandler(string registryName, Uri baseUri, bool allowInsecure, bool allowAutoRedirect, ILogger logger)
    {
        var socketsHttpHandler = new SocketsHttpHandler
        {
            AllowAutoRedirect = allowAutoRedirect,
            UseCookies = false,
            ConnectTimeout = TimeSpan.FromSeconds(30),
        };

        if (!allowInsecure)
        {
            return socketsHttpHandler;
        }

        socketsHttpHandler.SslOptions = new SslClientAuthenticationOptions
        {
            RemoteCertificateValidationCallback = IgnoreCertificateErrorsForSpecificHost(baseUri.Host),
        };

        return new FallbackToHttpMessageHandler(registryName, baseUri.Host, baseUri.Port, socketsHttpHandler, logger);
    }

    private static RemoteCertificateValidationCallback IgnoreCertificateErrorsForSpecificHost(string host)
    {
        return (object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors) =>
            sslPolicyErrors == SslPolicyErrors.None || (sender as SslStream)?.TargetHostName == host;
    }
}
