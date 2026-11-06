// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Credentials;
using Microsoft.NET.Build.Containers.Resources;
using Valleysoft.DockerCredsProvider;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// A delegating handler that performs the Docker auth handshake as described <see href="https://docs.docker.com/registry/spec/auth/token/">in their docs</see> if a request isn't authenticated
/// </summary>
internal sealed partial class AuthHandshakeMessageHandler : DelegatingHandler
{
    private const int MaxRequestRetries = 5; // Arbitrary but seems to work ok for chunked uploads to ghcr.io

    /// <summary>
    /// Unique identifier that is used to tag requests from this library to external registries.
    /// </summary>
    /// <remarks>
    /// Valid characters for this clientID are in the unicode range <see href="https://wintelguy.com/unicode_character_lookup.pl/?str=20-7E">20-7E</see>
    /// </remarks>
    private const string ClientID = "netsdkcontainers";
    private const string BasicAuthScheme = "Basic";
    private const string BearerAuthScheme = "Bearer";

    private sealed record AuthInfo(string Realm, string? Service, string? Scope);

    private readonly string _registryName;
    private readonly Uri _registryUri;
    private readonly bool _isInsecureRegistry;
    private readonly int _insecureRegistryHttpPort;
    private readonly ILogger _logger;
    private readonly RegistryMode _registryMode;
    private readonly Func<Uri, HttpMessageHandler> _createRealmHandler;
    private static ConcurrentDictionary<string, AuthenticationHeaderValue?> _authenticationHeaders = new();

    public AuthHandshakeMessageHandler(
        string registryName,
        Uri registryUri,
        bool isInsecureRegistry,
        HttpMessageHandler innerHandler,
        ILogger logger,
        RegistryMode mode,
        Func<Uri, HttpMessageHandler>? realmHandlerFactory = null) : base(innerHandler)
    {
        _registryName = registryName;
        _registryUri = registryUri;
        _isInsecureRegistry = isInsecureRegistry;
        _insecureRegistryHttpPort = RegistryNameContainsPort(registryName) ? registryUri.Port : 80;
        _logger = logger;
        _registryMode = mode;
        _createRealmHandler = realmHandlerFactory
            ?? new AuthRealmHandlerFactory(registryName, registryUri, isInsecureRegistry, logger).Create;
    }

    /// <summary>
    /// the www-authenticate header must have realm, service, and scope information, so this method parses it into that shape if present
    /// </summary>
    /// <param name="msg"></param>
    /// <param name="bearerAuthInfo"></param>
    /// <returns></returns>
    private static bool TryParseAuthenticationInfo(HttpResponseMessage msg, [NotNullWhen(true)] out string? scheme, out AuthInfo? bearerAuthInfo)
    {
        bearerAuthInfo = null;
        scheme = null;

        var authenticateHeader = msg.Headers.WwwAuthenticate;
        if (!authenticateHeader.Any())
        {
            return false;
        }

        AuthenticationHeaderValue header = authenticateHeader.First();

        if (header.Scheme is not null)
        {
            scheme = header.Scheme;

            if (header.Scheme.Equals(BasicAuthScheme, StringComparison.OrdinalIgnoreCase))
            {
                bearerAuthInfo = null;
                return true;
            }
            else if (header.Scheme.Equals(BearerAuthScheme, StringComparison.OrdinalIgnoreCase))
            {
                var keyValues = ParseBearerArgs(header.Parameter);
                if (keyValues is null)
                {
                    return false;
                }
                return TryParseBearerAuthInfo(keyValues, out bearerAuthInfo);
            }
            else
            {
                return false;
            }
        }
        return false;

        static bool TryParseBearerAuthInfo(Dictionary<string, string> authValues, [NotNullWhen(true)] out AuthInfo? authInfo)
        {
            if (authValues.TryGetValue("realm", out string? realm))
            {
                string? service = null;
                authValues.TryGetValue("service", out service);
                string? scope = null;
                authValues.TryGetValue("scope", out scope);
                authInfo = new AuthInfo(realm, service, scope);
                return true;
            }
            else
            {
                authInfo = null;
                return false;
            }
        }

        static Dictionary<string, string>? ParseBearerArgs(string? bearerHeaderArgs)
        {
            if (bearerHeaderArgs is null)
            {
                return null;
            }
            Dictionary<string, string> keyValues = new();
            foreach (Match match in BearerParameterSplitter().Matches(bearerHeaderArgs))
            {
                keyValues.Add(match.Groups["key"].Value, match.Groups["value"].Value);
            }
            return keyValues;
        }
    }

    /// <summary>
    /// Response to a request to get a token using some auth.
    /// </summary>
    /// <remarks>
    /// <see href="https://docs.docker.com/registry/spec/auth/token/#token-response-fields"/>
    /// </remarks>
    private sealed record TokenResponse(string? token, string? access_token, int? expires_in, DateTimeOffset? issued_at)
    {
        public string ResolvedToken => token ?? access_token ?? throw new ArgumentException(Resource.GetString(nameof(Strings.InvalidTokenResponse)));
        public DateTimeOffset ResolvedExpiration
        {
            get
            {
                var issueTime = this.issued_at ?? DateTimeOffset.UtcNow; // per spec, if no issued_at use the current time
                var validityDuration = this.expires_in ?? 60; // per spec, if no expires_in use 60 seconds
                var expirationTime = issueTime.AddSeconds(validityDuration);
                return expirationTime;
            }
        }
    }

    /// <summary>
    /// Uses the authentication information from a 401 response to perform the authentication dance for a given registry.
    /// Credentials for the request are retrieved from the credential provider, then used to acquire a token.
    /// That token is cached for some duration determined by the authentication mechanism on a per-host basis.
    /// </summary>
    private async Task<(AuthenticationHeaderValue, DateTimeOffset)?> GetAuthenticationAsync(string registry, string scheme, AuthInfo? bearerAuthInfo, CancellationToken cancellationToken)
    {
        // For bearer auth, validate the realm before any credential lookup so that a malicious or
        // compromised registry response cannot trigger credential retrieval toward a hostile token
        // endpoint, and so that validation errors aren't masked by credential errors. The addresses
        // the realm resolves to are checked by the realm handler when it connects.
        Uri? validatedBearerRealm = null;
        HttpMessageHandler? realmHandler = null;
        if (scheme.Equals(BearerAuthScheme, StringComparison.OrdinalIgnoreCase))
        {
            Debug.Assert(bearerAuthInfo is not null);
            validatedBearerRealm = ValidateRealmUri(bearerAuthInfo.Realm, registry, _isInsecureRegistry);
            realmHandler = _createRealmHandler(validatedBearerRealm);
        }

        // The realm is untrusted, so token requests use a separate SSRF-protected transport rather
        // than the registry transport.
        using HttpMessageInvoker? realmClient = realmHandler is null ? null : new(realmHandler, disposeHandler: true);

        DockerCredentials? privateRepoCreds;
        // Allow overrides for auth via environment variables
        if (GetDockerCredentialsFromEnvironment(_registryMode) is (string credU, string credP))
        {
            privateRepoCreds = new DockerCredentials(credU, credP);
        }
        else
        {
            privateRepoCreds = await GetLoginCredentials(registry).ConfigureAwait(false);
        }

        if (scheme.Equals(BasicAuthScheme, StringComparison.OrdinalIgnoreCase))
        {
            var authValue = new AuthenticationHeaderValue(BasicAuthScheme, Convert.ToBase64String(Encoding.ASCII.GetBytes($"{privateRepoCreds.Username}:{privateRepoCreds.Password}")));
            return new(authValue, DateTimeOffset.MaxValue);
        }
        else if (scheme.Equals(BearerAuthScheme, StringComparison.OrdinalIgnoreCase))
        {
            Debug.Assert(bearerAuthInfo is not null);
            Debug.Assert(validatedBearerRealm is not null);
            Debug.Assert(realmClient is not null);

            // Obtain a Bearer token, when the credentials are:
            // - an identity token: use it for OAuth
            // - a username/password: use them for Basic auth, and fall back to OAuth

            if (string.IsNullOrWhiteSpace(privateRepoCreds.IdentityToken))
            {
                var authenticationValueAndDuration = await TryTokenGetAsync(realmClient, privateRepoCreds, bearerAuthInfo, validatedBearerRealm, cancellationToken).ConfigureAwait(false);
                if (authenticationValueAndDuration is not null)
                {
                    return authenticationValueAndDuration;
                }
            }

            return await TryOAuthPostAsync(realmClient, privateRepoCreds, bearerAuthInfo, validatedBearerRealm, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            return null;
        }
    }

    /// <summary>
    /// Validates the syntax and scheme of the bearer realm URL returned in a WWW-Authenticate
    /// challenge. The addresses the realm resolves to are checked by <see cref="AuthRealmHandlerFactory"/>.
    /// </summary>
    internal static Uri ValidateRealmUri(string realm, string registryName, bool isInsecureRegistry)
    {
        if (!Uri.TryCreate(realm, UriKind.Absolute, out Uri? realmUri))
        {
            throw new InvalidAuthResponseException(
                registryName,
                Resource.FormatString(nameof(Strings.InvalidAuthResponse_RelativeOrUnparseableRealm), realm));
        }

        // Scheme allowlist. Always permit https; permit http only when the registry is insecure.
        // (Uri.Scheme is normalized to lowercase by Uri itself, so a plain comparison is fine.)
        bool schemeAllowed = realmUri.Scheme switch
        {
            "https" => true,
            "http" => isInsecureRegistry,
            _ => false,
        };
        if (!schemeAllowed)
        {
            throw new InvalidAuthResponseException(
                registryName,
                Resource.FormatString(nameof(Strings.InvalidAuthResponse_DisallowedScheme), realm, realmUri.Scheme));
        }

        return realmUri;
    }

    private static bool RegistryNameContainsPort(string registryName)
    {
        // Use a scheme that does not have a default port.
        return new Uri($"container://{registryName}").Port != -1;
    }

    /// <summary>
    /// Returns whether <paramref name="uri"/> targets the configured registry.
    /// For a registry configured as insecure, the HTTP fallback produced by
    /// <see cref="FallbackToHttpMessageHandler"/> is also accepted so that fallback can authenticate.
    /// This exception is limited to the same host and fallback port; it does not permit general
    /// HTTP or cross-host redirects.
    /// </summary>
    private bool IsRegistryOrigin(Uri uri)
    {
        if (!uri.IsAbsoluteUri
            || !uri.IdnHost.Equals(_registryUri.IdnHost, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (uri.Scheme == _registryUri.Scheme && uri.Port == _registryUri.Port)
        {
            return true;
        }

        return _isInsecureRegistry
            && _registryUri.Scheme == Uri.UriSchemeHttps
            && uri.Scheme == Uri.UriSchemeHttp
            && uri.Port == _insecureRegistryHttpPort;
    }

    /// <summary>
    /// Rejects Basic or Bearer authentication challenges from an origin other than the configured registry.
    /// Disposes <paramref name="response"/> before throwing <see cref="InvalidAuthResponseException"/>
    /// on rejection; callers retain ownership of the response when this method returns normally.
    /// </summary>
    private void ValidateAuthenticationOrigin(HttpResponseMessage response, Uri requestUri)
    {
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return;
        }

        Uri challengeUri = response.RequestMessage?.RequestUri ?? requestUri;
        if (IsRegistryOrigin(challengeUri))
        {
            return;
        }

        // Reject another origin based on the scheme alone; its parameters may be malformed.
        if (response.Headers.WwwAuthenticate.Any(challenge =>
            string.Equals(challenge.Scheme, BasicAuthScheme, StringComparison.OrdinalIgnoreCase)
            || string.Equals(challenge.Scheme, BearerAuthScheme, StringComparison.OrdinalIgnoreCase)))
        {
            response.Dispose();
            throw new InvalidAuthResponseException(
                _registryName,
                Resource.FormatString(
                    nameof(Strings.InvalidAuthResponse_UnexpectedAuthOrigin),
                    challengeUri.GetLeftPart(UriPartial.Authority),
                    _registryUri.GetLeftPart(UriPartial.Authority)));
        }
    }


    internal static (string credU, string credP)? TryGetCredentialsFromEnvVars(string unameVar, string passwordVar)
    {
        var credU = Environment.GetEnvironmentVariable(unameVar);
        var credP = Environment.GetEnvironmentVariable(passwordVar);
        if (!string.IsNullOrEmpty(credU) && !string.IsNullOrEmpty(credP))
        {
            return (credU, credP);
        }
        else
        {
            return null;
        }
    }

    /// <summary>
    /// Gets docker credentials from the environment variables based on registry mode.
    /// </summary>
    internal static (string credU, string credP)? GetDockerCredentialsFromEnvironment(RegistryMode mode)
    {
        if (mode == RegistryMode.Push)
        {
            if (TryGetCredentialsFromEnvVars(ContainerHelpers.PushHostObjectUser, ContainerHelpers.PushHostObjectPass) is (string, string) pushCreds)
            {
                return pushCreds;
            }

            if (TryGetCredentialsFromEnvVars(ContainerHelpers.HostObjectUser, ContainerHelpers.HostObjectPass) is (string, string) genericCreds)
            {
                return genericCreds;
            }

            return TryGetCredentialsFromEnvVars(ContainerHelpers.HostObjectUserLegacy, ContainerHelpers.HostObjectPassLegacy);
        }
        else if (mode == RegistryMode.Pull)
        {
            return TryGetCredentialsFromEnvVars(ContainerHelpers.PullHostObjectUser, ContainerHelpers.PullHostObjectPass);
        }
        else if (mode == RegistryMode.PullFromOutput)
        {
            if (TryGetCredentialsFromEnvVars(ContainerHelpers.PullHostObjectUser, ContainerHelpers.PullHostObjectPass) is (string, string) pullCreds)
            {
                return pullCreds;
            }

            if (TryGetCredentialsFromEnvVars(ContainerHelpers.HostObjectUser, ContainerHelpers.HostObjectPass) is (string, string) genericCreds)
            {
                return genericCreds;
            }

            return TryGetCredentialsFromEnvVars(ContainerHelpers.HostObjectUserLegacy, ContainerHelpers.HostObjectPassLegacy);
        }
        else
        {
            throw new InvalidEnumArgumentException(nameof(mode), (int)mode, typeof(RegistryMode));
        }
    }

    /// <summary>
    /// Implements the Docker OAuth2 Authentication flow as documented at <see href="https://docs.docker.com/registry/spec/auth/oauth/"/>.
    /// </summary
    private async Task<(AuthenticationHeaderValue, DateTimeOffset)?> TryOAuthPostAsync(HttpMessageInvoker realmClient, DockerCredentials privateRepoCreds, AuthInfo bearerAuthInfo, Uri realmUri, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogTrace("Attempting to authenticate on {uri} using POST.", realmUri);
        Dictionary<string, string?> parameters = new()
        {
            ["client_id"] = ClientID,
        };
        if (!string.IsNullOrWhiteSpace(privateRepoCreds.IdentityToken))
        {
            parameters["grant_type"] = "refresh_token";
            parameters["refresh_token"] = privateRepoCreds.IdentityToken;
        }
        else
        {
            parameters["grant_type"] = "password";
            parameters["username"] = privateRepoCreds.Username;
            parameters["password"] = privateRepoCreds.Password;
        }
        if (bearerAuthInfo.Service is not null)
        {
            parameters["service"] = bearerAuthInfo.Service;
        }
        if (bearerAuthInfo.Scope is not null)
        {
            parameters["scope"] = bearerAuthInfo.Scope;
        };
        HttpRequestMessage postMessage = new(HttpMethod.Post, realmUri)
        {
            Content = new FormUrlEncodedContent(parameters)
        };

        using HttpResponseMessage postResponse = await realmClient.SendAsync(postMessage, cancellationToken).ConfigureAwait(false);
        if (!postResponse.IsSuccessStatusCode)
        {
            await postResponse.LogHttpResponseAsync(_logger, cancellationToken).ConfigureAwait(false);
            return null; // try next method
        }
        _logger.LogTrace("Received '{statuscode}'.", postResponse.StatusCode);
        TokenResponse? tokenResponse = JsonSerializer.Deserialize<TokenResponse>(postResponse.Content.ReadAsStream(cancellationToken));
        if (tokenResponse is { } tokenEnvelope)
        {
            var authValue = new AuthenticationHeaderValue(BearerAuthScheme, tokenResponse.ResolvedToken);
            return (authValue, tokenResponse.ResolvedExpiration);
        }
        else
        {
            _logger.LogTrace(Resource.GetString(nameof(Strings.CouldntDeserializeJsonToken)));
            return null; // try next method
        }
    }

    /// <summary>
    /// Implements the Docker Token Authentication flow as documented at <see href="https://docs.docker.com/registry/spec/auth/token/"/>
    /// </summary>
    private async Task<(AuthenticationHeaderValue, DateTimeOffset)?> TryTokenGetAsync(HttpMessageInvoker realmClient, DockerCredentials privateRepoCreds, AuthInfo bearerAuthInfo, Uri realmUri, CancellationToken cancellationToken)
    {
        // this doesn't seem to be called out in the spec, but actual username/password auth information should be converted into Basic auth here,
        // even though the overall Scheme we're authenticating for is Bearer
        var header = new AuthenticationHeaderValue(BasicAuthScheme, Convert.ToBase64String(Encoding.ASCII.GetBytes($"{privateRepoCreds.Username}:{privateRepoCreds.Password}")));
        var builder = new UriBuilder(realmUri);

        _logger.LogTrace("Attempting to authenticate on {uri} using GET.", realmUri);
        var queryDict = System.Web.HttpUtility.ParseQueryString("");
        if (bearerAuthInfo.Service is string svc)
        {
            queryDict["service"] = svc;
        }
        if (bearerAuthInfo.Scope is string s)
        {
            queryDict["scope"] = s;
        }
        builder.Query = queryDict.ToString();
        var message = new HttpRequestMessage(HttpMethod.Get, builder.ToString());
        message.Headers.Authorization = header;

        using var tokenResponse = await realmClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            await tokenResponse.LogHttpResponseAsync(_logger, cancellationToken).ConfigureAwait(false);
            return null; // try next method
        }

        TokenResponse? token = JsonSerializer.Deserialize<TokenResponse>(tokenResponse.Content.ReadAsStream(cancellationToken));
        if (token is null)
        {
            throw new ArgumentException(Resource.GetString(nameof(Strings.CouldntDeserializeJsonToken)));
        }
        return (new AuthenticationHeaderValue(BearerAuthScheme, token.ResolvedToken), token.ResolvedExpiration);
    }

    private static async Task<DockerCredentials> GetLoginCredentials(string registry)
    {
        // For authentication with Docker Hub, 'docker login' uses 'https://index.docker.io/v1/' as the registry key.
        // And 'podman login docker.io' uses 'docker.io'.
        // Try the key used by 'docker' first, and then fall back to the regular case for 'podman'.
        if (registry == ContainerHelpers.DockerRegistryAlias)
        {
            try
            {
                return await CredsProvider.GetCredentialsAsync("https://index.docker.io/v1/").ConfigureAwait(false);
            }
            catch
            { }
        }

        try
        {
            return await CredsProvider.GetCredentialsAsync(registry).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            throw new CredentialRetrievalException(registry, e);
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is null)
        {
            throw new ArgumentException(Resource.GetString(nameof(Strings.NoRequestUriSpecified)), nameof(request));
        }

        if (IsRegistryOrigin(request.RequestUri)
            && _authenticationHeaders.TryGetValue(_registryName, out AuthenticationHeaderValue? header))
        {
            request.Headers.Authorization = header;
        }

        int retryCount = 0;
        List<Exception>? requestExceptions = null;

        while (retryCount < MaxRequestRetries)
        {
            try
            {
                var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                ValidateAuthenticationOrigin(response, request.RequestUri);
                if (response is { StatusCode: HttpStatusCode.OK })
                {
                    return response;
                }
                else if (response is { StatusCode: HttpStatusCode.Unauthorized } && TryParseAuthenticationInfo(response, out string? scheme, out AuthInfo? authInfo))
                {
                    // Load the reply so the HTTP connection becomes available to send the authentication request.
                    // Ideally we'd call LoadIntoBufferAsync, but it has no overload that accepts a CancellationToken so we call ReadAsByteArrayAsync instead.
                    _ = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

                    if (await GetAuthenticationAsync(_registryName, scheme!, authInfo, cancellationToken).ConfigureAwait(false) is (AuthenticationHeaderValue authHeader, DateTimeOffset expirationTime))
                    {
                        _authenticationHeaders[_registryName] = authHeader;
                        request.Headers.Authorization = authHeader;
                        HttpResponseMessage authenticatedResponse = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                        ValidateAuthenticationOrigin(authenticatedResponse, request.RequestUri);
                        return authenticatedResponse;
                    }

                    throw new UnableToAccessRepositoryException(_registryName);
                }
                else
                {
                    return response;
                }
            }
            catch (HttpRequestException e) when (e.InnerException is IOException ioe && ioe.InnerException is SocketException se)
            {
                requestExceptions ??= new();
                requestExceptions.Add(e);

                retryCount += 1;
                _logger.LogInformation("Encountered a HttpRequestException {error} with message \"{message}\". Pausing before retry.", e.HttpRequestError, se.Message);
                _logger.LogTrace("Exception details: {ex}", se);
                await Task.Delay(TimeSpan.FromSeconds(1.0 * Math.Pow(2, retryCount)), cancellationToken).ConfigureAwait(false);

                // retry
                continue;
            }
        }

        throw new ApplicationException(Resource.GetString(nameof(Strings.TooManyRetries)), new AggregateException(requestExceptions!));
    }

    [GeneratedRegex("(?<key>\\w+)=\"(?<value>[^\"]*)\"(?:,|$)")]
    private static partial Regex BearerParameterSplitter();
}
