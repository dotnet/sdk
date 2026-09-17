// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using OrasProject.Oras.Registry.Remote.Auth;

namespace Microsoft.NET.Build.Containers;

internal sealed class DefaultRegistryAPI : IRegistryAPI
{
    // Empirical value - Unoptimized .NET application layers can be ~200MB
    // * .NET Runtime (~80MB)
    // * ASP.NET Runtime (~25MB)
    // * application and dependencies - variable, but _probably_ not more than the BCL?
    // Given a 200MB target and a 1Mb/s upload speed, we'd expect an upload speed of 27m:57s.
    // Making this a round 30 for convenience.
    private static TimeSpan LongRequestTimeout = TimeSpan.FromMinutes(30);

    internal DefaultRegistryAPI(string registryName, Uri baseUri, RegistrySettings settings, ILogger logger, RegistryMode mode)
    {
        bool isInsecureRegistry = settings.IsInsecure;
        Client orasClient = new(
            CreateHttpClient(registryName, baseUri, logger, isInsecureRegistry, allowAutoRedirect: true),
            CreateHttpClient(registryName, baseUri, logger, isInsecureRegistry, allowAutoRedirect: false),
            new OrasCredentialProvider(mode),
            accessTokenProvider: null,
            cache: null)
        {
            ClientId = "netsdkcontainers",
            RealmValidator = new OrasRealmValidator(registryName, isInsecureRegistry),
        };
        orasClient.SetUserAgent($".NET Container Library v{Constants.Version}");

        OrasRepositoryFactory repositoryFactory = new(baseUri, orasClient, settings);
        Manifest = new DefaultManifestOperations(repositoryFactory, registryName, logger);
        Blob = new DefaultBlobOperations(repositoryFactory, registryName, logger);
    }

    public IBlobOperations Blob { get; }

    public IManifestOperations Manifest { get; }

    private static HttpClient CreateHttpClient(string registryName, Uri baseUri, ILogger logger, bool isInsecureRegistry, bool allowAutoRedirect)
    {
        HttpMessageHandler clientHandler = CreateHttpHandler(registryName, baseUri, isInsecureRegistry, allowAutoRedirect, logger);

        if (baseUri.IsAmazonECRRegistry())
        {
            clientHandler = new AmazonECRMessageHandler(clientHandler);
        }

        return new HttpClient(clientHandler)
        {
            Timeout = LongRequestTimeout
        };
    }

    private static HttpMessageHandler CreateHttpHandler(string registryName, Uri baseUri, bool allowInsecure, bool allowAutoRedirect, ILogger logger)
    {
        var socketsHttpHandler = new SocketsHttpHandler()
        {
            AllowAutoRedirect = allowAutoRedirect,
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
