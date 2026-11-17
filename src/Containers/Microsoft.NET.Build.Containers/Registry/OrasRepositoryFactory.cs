// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Resources;
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
    // Preserve the SDK's ECR default when the registry does not advertise a minimum.
    private const int DefaultEcrChunkSizeBytes = 5_248_080;
    private static readonly TimeSpan s_longRequestTimeout = TimeSpan.FromMinutes(30);

    private readonly Uri _baseUri;
    private readonly IClient _client;
    private readonly RegistrySettings _settings;
    private readonly string _partitionId = Guid.NewGuid().ToString("N");

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
        BlobUploadChunkSize = _settings.ChunkedUploadSizeBytes ?? (_baseUri.IsAmazonECRRegistry() ? DefaultEcrChunkSizeBytes : DefaultChunkSizeBytes),
        BlobUploadMode = _settings.ForceChunkedUpload ?
            BlobUploadMode.Chunked :
            BlobUploadMode.MonolithicWithChunkedFallback,
        Client = _client,
        PartitionId = _partitionId,
        PlainHttp = _baseUri.Scheme == Uri.UriSchemeHttp,
        Reference = Reference.Parse($"{_baseUri.Authority}/{repositoryName}"),
    });

    internal static Client CreateClient(string registryName, Uri baseUri, RegistrySettings settings, ILogger logger, RegistryMode mode,
        HttpMessageHandler? registryHandler = null, Func<Uri, HttpMessageHandler>? realmHandlerFactory = null)
    {
        bool isInsecureRegistry = settings.IsInsecure;
        Client client = new(
            CreateHttpClient(registryName, baseUri, logger, isInsecureRegistry, allowAutoRedirect: true, registryHandler),
            CreateHttpClient(registryName, baseUri, logger, isInsecureRegistry, allowAutoRedirect: false),
            new OrasCredentialProvider(mode, baseUri.Authority),
            accessTokenProvider: null,
            cache: null)
        {
            ClientId = "netsdkcontainers",
            AttemptOAuth2Fallback = true,
            TokenClient = new HttpClient(new RealmTransportHandler(realmHandlerFactory ??
                new AuthRealmHandlerFactory(registryName, baseUri, isInsecureRegistry).Create)),
            RealmValidator = new OrasRealmValidator(registryName, isInsecureRegistry),
        };
        client.SetUserAgent($".NET Container Library v{Constants.Version}");
        return client;
    }

    private static HttpClient CreateHttpClient(string registryName, Uri baseUri, ILogger logger, bool isInsecureRegistry,
        bool allowAutoRedirect, HttpMessageHandler? registryHandler = null)
    {
        HttpMessageHandler clientHandler = registryHandler ??
            CreateHttpHandler(registryName, baseUri, isInsecureRegistry, allowAutoRedirect, logger);

        return new HttpClient(new RegistryOriginHandler(registryName, baseUri, isInsecureRegistry, clientHandler))
        {
            Timeout = s_longRequestTimeout,
        };
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

    internal static RemoteCertificateValidationCallback IgnoreCertificateErrorsForSpecificHost(string host)
    {
        return (object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors) =>
            sslPolicyErrors == SslPolicyErrors.None || (sender as SslStream)?.TargetHostName == host;
    }

    private sealed class RealmTransportHandler(Func<Uri, HttpMessageHandler> create) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var transport = new HttpMessageInvoker(create(request.RequestUri!));
            var response = await transport.SendAsync(request, cancellationToken).ConfigureAwait(false);
            try
            {
                // Buffer the token before disposing its dedicated transport.
                await response.Content.LoadIntoBufferAsync(cancellationToken).ConfigureAwait(false);
                return response;
            }
            catch
            {
                response.Dispose();
                throw;
            }
        }
    }

    private sealed class RegistryOriginHandler(string registryName, Uri registryUri, bool insecure, HttpMessageHandler inner)
        : DelegatingHandler(inner)
    {
        private bool IsRegistryOrigin(Uri uri) => uri.IdnHost.Equals(registryUri.IdnHost, StringComparison.OrdinalIgnoreCase) &&
            ((uri.Scheme == registryUri.Scheme && uri.Port == registryUri.Port) ||
            (insecure && registryUri.Scheme == Uri.UriSchemeHttps && uri.Scheme == Uri.UriSchemeHttp &&
                uri.Port == (new Uri($"container://{registryName}").Port == -1 ? 80 : registryUri.Port)));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri originalUri = request.RequestUri!;
            if (!IsRegistryOrigin(originalUri))
            {
                request.Headers.Authorization = null;
            }
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            Uri effectiveUri = response.RequestMessage?.RequestUri ?? originalUri;
            if (response.StatusCode == HttpStatusCode.Unauthorized && !IsRegistryOrigin(effectiveUri) &&
                response.Headers.WwwAuthenticate.Any(challenge =>
                    challenge.Scheme.Equals("Basic", StringComparison.OrdinalIgnoreCase) ||
                    challenge.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase)))
            {
                response.Dispose();
                throw new InvalidAuthResponseException(registryName,
                    Resource.FormatString(nameof(Strings.InvalidAuthResponse_UnexpectedAuthOrigin),
                        effectiveUri.GetLeftPart(UriPartial.Authority), registryUri.GetLeftPart(UriPartial.Authority)));
            }
            return response;
        }
    }
}
