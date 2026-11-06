// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Resources;
using Microsoft.Security.AntiSSRF;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Creates the HTTP transport used to request bearer tokens from the realm named in a registry's
/// authentication challenge.
/// </summary>
/// <remarks>
/// The realm is supplied by the registry, so it is untrusted. Token requests are sent through an
/// <see cref="AntiSSRFHandler"/>, which resolves the host when each connection is opened (including
/// connections made for redirects and connections to a proxy) and refuses the connection if any resolved
/// address is disallowed by the policy. The policy is configured only by
/// <see cref="RegistrySettings.EnvVariables.AuthRealmPolicy"/> and
/// <see cref="RegistrySettings.EnvVariables.AuthRealmAllowedAddresses"/>.
/// </remarks>
internal sealed class AuthRealmHandlerFactory
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(30);

    private readonly string _registryName;
    private readonly Uri _registryUri;
    private readonly bool _isInsecureRegistry;
    private readonly ILogger _logger;
    private readonly IEnvironmentProvider _environment;

    public AuthRealmHandlerFactory(string registryName, Uri registryUri, bool isInsecureRegistry, ILogger logger, IEnvironmentProvider? environment = null)
    {
        _registryName = registryName;
        _registryUri = registryUri;
        _isInsecureRegistry = isInsecureRegistry;
        _logger = logger;
        _environment = environment ?? new EnvironmentProvider();
    }

    /// <summary>
    /// Creates a handler for token requests to <paramref name="realmUri"/>. The caller owns the handler.
    /// </summary>
    /// <exception cref="ArgumentException">An SSRF policy environment variable has an invalid value.</exception>
    public HttpMessageHandler Create(Uri realmUri)
    {
        AntiSSRFHandler antiSsrfHandler = CreatePolicy().GetHandler();
        antiSsrfHandler.UseCookies = false;
        antiSsrfHandler.ConnectTimeout = ConnectTimeout;
        // RedirectHandler follows redirects instead, and each hop still passes through AntiSSRFHandler's
        // address checks. Do not use FallbackToHttpMessageHandler here: an insecure registry may use HTTP
        // for its realm explicitly, but an HTTPS realm must never be retried over plaintext.
        antiSsrfHandler.AllowAutoRedirect = false;

        HttpMessageHandler handler = new RedirectHandler(new ExceptionTranslationHandler(antiSsrfHandler, _registryName, realmUri), _logger);
        if (!_isInsecureRegistry)
        {
            return handler;
        }

        // Match the registry transport for insecure registries by tolerating certificate errors from
        // the registry host. Unlike the registry transport, this never falls back from https to http:
        // http is only used when the realm URI itself is http (AllowPlainTextHttp above).
        antiSsrfHandler.SslOptions = new SslClientAuthenticationOptions
        {
            RemoteCertificateValidationCallback = DefaultRegistryAPI.IgnoreCertificateErrorsForSpecificHost(_registryUri.Host)
        };
        return handler;
    }

    /// <summary>
    /// Creates the AntiSSRF policy from the environment.
    /// </summary>
    /// <exception cref="ArgumentException">An SSRF policy environment variable has an invalid value.</exception>
    internal AntiSSRFPolicy CreatePolicy()
    {
        var policy = new AntiSSRFPolicy(ReadPolicyOption())
        {
            AllowPlainTextHttp = _isInsecureRegistry,
            // Send token requests unchanged; an 'X-Forwarded-For: true' header can be rejected by
            // intermediaries that expect an address.
            AddXFFHeader = false,
        };

        string? allowedAddresses = _environment.GetEnvironmentVariable(RegistrySettings.EnvVariables.AuthRealmAllowedAddresses);
        if (allowedAddresses is not null)
        {
            foreach (string entry in allowedAddresses.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                try
                {
                    policy.AddAllowedAddresses([entry]);
                }
                catch (FormatException)
                {
                    throw new InvalidAuthRealmConfigurationException(
                        nameof(Strings.InvalidAuthRealmAllowedAddress),
                        RegistrySettings.EnvVariables.AuthRealmAllowedAddresses,
                        entry);
                }
            }
        }

        return policy;
    }

    private PolicyConfigOptions ReadPolicyOption()
    {
        string? value = _environment.GetEnvironmentVariable(RegistrySettings.EnvVariables.AuthRealmPolicy)?.Trim();
        if (string.IsNullOrEmpty(value) || value.Equals("Public", StringComparison.OrdinalIgnoreCase))
        {
            return PolicyConfigOptions.ExternalOnlyLatest;
        }
        if (value.Equals("AllowlistOnly", StringComparison.OrdinalIgnoreCase))
        {
            return PolicyConfigOptions.InternalOnly;
        }
        if (value.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            return PolicyConfigOptions.None;
        }

        throw new InvalidAuthRealmConfigurationException(
            nameof(Strings.InvalidAuthRealmPolicy),
            RegistrySettings.EnvVariables.AuthRealmPolicy,
            value);
    }

    /// <summary>
    /// Follows redirects like <see cref="SocketsHttpHandler"/> does, including never following a redirect
    /// from https to http, which would send the token request (and any credentials in its body) in clear text.
    /// </summary>
    internal sealed class RedirectHandler(HttpMessageHandler innerHandler, ILogger logger) : DelegatingHandler(innerHandler)
    {
        private const int MaxRedirects = 50;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            for (int redirectCount = 0; redirectCount < MaxRedirects; redirectCount++)
            {
                Uri requestUri = request.RequestUri!;
                if (GetRedirectUri(requestUri, response) is not Uri redirectUri)
                {
                    break;
                }

                if (requestUri.Scheme == Uri.UriSchemeHttps && redirectUri.Scheme != Uri.UriSchemeHttps)
                {
                    logger.LogTrace("Not following insecure redirect from {from} to {to}.", requestUri, redirectUri);
                    break;
                }

                HttpStatusCode statusCode = response.StatusCode;
                response.Dispose();

                request.RequestUri = redirectUri;
                request.Headers.Authorization = null;
                if (RedirectChangesMethodToGet(request.Method, statusCode))
                {
                    request.Method = HttpMethod.Get;
                    request.Content = null;
                    request.Headers.TransferEncodingChunked = false;
                }

                response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }

            return response;
        }

        private static Uri? GetRedirectUri(Uri requestUri, HttpResponseMessage response)
        {
            if (response.StatusCode is not (HttpStatusCode.MultipleChoices or HttpStatusCode.MovedPermanently or HttpStatusCode.Found
                    or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
                || response.Headers.Location is not Uri location)
            {
                return null;
            }

            if (!location.IsAbsoluteUri)
            {
                location = new Uri(requestUri, location);
            }

            return location.Scheme == Uri.UriSchemeHttps || location.Scheme == Uri.UriSchemeHttp ? location : null;
        }

        private static bool RedirectChangesMethodToGet(HttpMethod method, HttpStatusCode statusCode) =>
            statusCode switch
            {
                HttpStatusCode.MultipleChoices or HttpStatusCode.MovedPermanently or HttpStatusCode.Found => method == HttpMethod.Post,
                HttpStatusCode.SeeOther => method != HttpMethod.Get && method != HttpMethod.Head,
                _ => false,
            };
    }

    /// <summary>
    /// Reports AntiSSRF policy violations as <see cref="InvalidAuthResponseException"/>, and restores the
    /// <see cref="HttpRequestException"/> that <see cref="AntiSSRFHandler"/> unwraps so that the https
    /// fallback and retry logic see the usual transport exceptions.
    /// </summary>
    internal sealed class ExceptionTranslationHandler(HttpMessageHandler innerHandler, string registryName, Uri realmUri)
        : DelegatingHandler(innerHandler)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            try
            {
                return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (AntiSSRFException ex)
            {
                throw new InvalidAuthResponseException(
                    registryName,
                    Resource.FormatString(
                        nameof(Strings.InvalidAuthResponse_RealmBlockedBySsrfPolicy),
                        realmUri,
                        ex.Message,
                        RegistrySettings.EnvVariables.AuthRealmAllowedAddresses,
                        RegistrySettings.EnvVariables.AuthRealmPolicy),
                    ex);
            }
            catch (AuthenticationException ex)
            {
                throw new HttpRequestException(HttpRequestError.SecureConnectionError, ex.Message, ex);
            }
            catch (SocketException ex)
            {
                HttpRequestError error = ex.SocketErrorCode is SocketError.HostNotFound or SocketError.NoData or SocketError.TryAgain
                    ? HttpRequestError.NameResolutionError
                    : HttpRequestError.ConnectionError;
                throw new HttpRequestException(error, ex.Message, ex);
            }
            catch (IOException ex)
            {
                HttpRequestError error = ex is HttpIOException httpException
                    ? httpException.HttpRequestError
                    : HttpRequestError.Unknown;
                throw new HttpRequestException(error, ex.Message, ex);
            }
        }
    }
}
