// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Registry/Remote/Auth/Client.cs
// Copyright The ORAS Authors.
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Runtime.ExceptionServices;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OrasProject.Oras.Registry.Remote.Exceptions;

namespace OrasProject.Oras.Registry.Remote.Auth;

/// <summary>
/// Client provides authenticated access to OCI registries with automatic token management.
/// </summary>
public class Client : IClient
{
    #region private members
    /// <summary>
    /// Lazy singleton memory cache for scenarios where no IMemoryCache is injected.
    /// </summary>
    private static readonly Lazy<IMemoryCache> _sharedMemoryCache =
        new(() => new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 1024, // cache at most 1024 entries
        }), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Cache used for storing and retrieving authentication tokens
    /// to optimize remote registry operations.
    /// </summary>
    private ICache? _cache;

    /// <summary>
    /// Object used for locking during cache initialization.
    /// </summary>
    private readonly object _cacheLock = new();
    #endregion

    /// <summary>
    /// Initializes a new instance of the Client class.
    /// </summary>
    /// <param name="httpClient">
    /// Optional HttpClient to use for standard requests that follow redirects.
    /// If not provided, uses <see cref="DefaultHttpClient.Instance"/>.
    /// </param>
    /// <param name="credentialProvider">Optional credential provider for registry authentication.</param>
    /// <param name="cache">Optional cache for storing authentication tokens.</param>
    public Client(
        HttpClient? httpClient = null,
        ICredentialProvider? credentialProvider = null,
        ICache? cache = null)
        : this(httpClient, null, credentialProvider, null, cache)
    {
    }

    /// <summary>
    /// Initializes a new instance of the Client class with separate
    /// HttpClient instances for redirect control.
    /// </summary>
    /// <param name="httpClient">
    /// Optional HttpClient to use for standard requests that follow
    /// redirects.  If not provided, uses
    /// <see cref="DefaultHttpClient.Instance"/>.
    /// </param>
    /// <param name="noRedirectHttpClient">
    /// Optional HttpClient configured with
    /// <c>AllowAutoRedirect = false</c> for capturing redirect
    /// locations.  If not provided, uses
    /// <see cref="DefaultHttpClient.NoRedirectInstance"/>.
    /// <para>
    /// <strong>Advanced Usage:</strong> To apply consistent HTTP
    /// configuration (timeouts, proxy, headers) across both redirect
    /// and no-redirect scenarios, provide both
    /// <paramref name="httpClient"/> and
    /// <paramref name="noRedirectHttpClient"/> with the same base
    /// configuration but different redirect settings.  This is useful
    /// with IHttpClientFactory or custom HttpClient management.
    /// </para>
    /// </param>
    /// <param name="credentialProvider">
    /// Optional credential provider for registry authentication.
    /// </param>
    /// <param name="cache">
    /// Optional cache for storing authentication tokens.
    /// </param>
    public Client(
        HttpClient? httpClient,
        HttpClient? noRedirectHttpClient,
        ICredentialProvider? credentialProvider,
        ICache? cache)
        : this(httpClient, noRedirectHttpClient, credentialProvider,
            null, cache)
    {
    }

    /// <summary>
    /// Initializes a new instance of the Client class with separate
    /// HttpClient instances for redirect control and an optional
    /// access-token provider.
    /// </summary>
    /// <param name="httpClient">
    /// Optional HttpClient to use for standard requests that follow
    /// redirects.  If not provided, uses
    /// <see cref="DefaultHttpClient.Instance"/>.
    /// </param>
    /// <param name="noRedirectHttpClient">
    /// Optional HttpClient configured with
    /// <c>AllowAutoRedirect = false</c> for capturing redirect
    /// locations.  If not provided, uses
    /// <see cref="DefaultHttpClient.NoRedirectInstance"/>.
    /// </param>
    /// <param name="credentialProvider">
    /// Optional credential provider for registry authentication.
    /// </param>
    /// <param name="accessTokenProvider">
    /// Optional access-token provider.  When set, the client calls
    /// this provider first during Bearer authentication.  If it
    /// returns <c>null</c> or whitespace, the client falls through to
    /// credential-based authentication via
    /// <paramref name="credentialProvider"/>.
    /// </param>
    /// <param name="cache">
    /// Optional cache for storing authentication tokens.
    /// </param>
    public Client(
        HttpClient? httpClient,
        HttpClient? noRedirectHttpClient,
        ICredentialProvider? credentialProvider,
        IAccessTokenProvider? accessTokenProvider,
        ICache? cache)
    {
        CredentialProvider = credentialProvider;
        AccessTokenProvider = accessTokenProvider;
        _cache = cache;
        BaseClient = httpClient ?? DefaultHttpClient.Instance;
        NoRedirectClient =
            noRedirectHttpClient ?? DefaultHttpClient.NoRedirectInstance;
    }

    /// <summary>
    /// CredentialProvider provides the mechanism to retrieve
    /// credentials for accessing remote registries.
    /// </summary>
    public ICredentialProvider? CredentialProvider { get; init; }

    /// <summary>
    /// AccessTokenProvider provides pre-resolved access tokens for
    /// Bearer authentication.  When set, the client calls this provider
    /// first during Bearer auth.  If it returns <c>null</c> or
    /// whitespace, the client falls through to credential-based
    /// authentication via <see cref="CredentialProvider"/>.
    /// </summary>
    public IAccessTokenProvider? AccessTokenProvider { get; init; }

    /// <summary>
    /// BaseClient is an instance of HttpClient to send http requests that follow redirects.
    /// </summary>
    public HttpClient BaseClient { get; }

    /// <summary>
    /// Optional transport for bearer-token requests, allowing separate redirect and network
    /// policies for untrusted authentication realms. When unset, BaseClient is used.
    /// The caller owns the supplied client and configures its redirect behavior.
    /// </summary>
    public HttpClient? TokenClient { get; init; }

    /// <summary>
    /// NoRedirectClient is an instance of HttpClient configured to not follow redirects.
    /// Used for scenarios where we need to capture redirect locations (e.g., blob location URLs).
    /// </summary>
    internal HttpClient NoRedirectClient { get; }

    /// <summary>
    /// Cache used for storing and retrieving authentication tokens.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If no <see cref="ICache"/> is provided during construction, a default <see cref="Cache"/> 
    /// implementation is created using a shared <see cref="MemoryCache"/> instance with a size 
    /// limit of 1024 entries.
    /// </para>
    /// <para>
    /// The shared memory cache uses a size-based eviction policy, where each cache entry counts
    /// as 1 unit of size by default. When the cache reaches its limit of 1024 entries, the least
    /// recently used entries will be evicted.
    /// </para>
    /// <para>
    /// To customize caching behavior, you can either:
    /// <list type="bullet">
    /// <item>
    /// <description>Provide your own <see cref="ICache"/> implementation in the constructor</description>
    /// </item>
    /// <item>
    /// <description>Configure <see cref="Cache.CacheEntryOptions"/> on the default implementation
    /// </description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public ICache Cache
    {
        get
        {
            if (_cache == null)
            {
                lock (_cacheLock)
                {
                    _cache ??= new Cache(_sharedMemoryCache.Value);
                }
            }
            return _cache;
        }
        set => _cache = value;
    }

    /// <summary>
    /// ClientId used in fetching OAuth2 token as a required field.
    /// If empty, a default client ID is used.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Determines whether to enforce OAuth2 authentication with the password grant type
    /// instead of using the distribution specification when authenticating with a username
    /// and password.
    /// </summary>
    public bool ForceAttemptOAuth2 { get; set; }

    /// <summary>
    /// When enabled, retry a rejected distribution-token GET with an OAuth2 password-grant
    /// POST for username/password credentials. Transport failures and redirects do not trigger
    /// this fallback. Disabled by default.
    /// </summary>
    public bool AttemptOAuth2Fallback { get; init; }

    /// <summary>
    /// defaultClientID specifies the default client ID used in OAuth2.
    /// </summary>
    private const string _defaultClientId = "oras-dotnet";

    /// <summary>
    /// The HTTP Authorization header name.
    /// </summary>
    private const string _authorizationHeader = "Authorization";

    /// <summary>
    /// Maximum size (4 MB) for buffering non-seekable request content.
    /// In practice only manifest-sized payloads flow through this path.
    /// </summary>
    internal const long MaxBufferSize = 4 * 1024 * 1024;

    /// <summary>
    /// Validates realm URLs before sending credentials to them.
    /// Default: a <see cref="DefaultRealmValidator"/> instance with
    /// secure defaults.
    /// </summary>
    /// <remarks>
    /// This property is init-only and cannot be null. To use a
    /// different validator, set it during construction. To disable
    /// validation (not recommended), provide an implementation
    /// that always returns <c>true</c>.
    /// </remarks>
    public IRealmValidator RealmValidator
    {
        get => _realmValidator;
        init => _realmValidator = value
            ?? throw new ArgumentNullException(nameof(value));
    }

    private IRealmValidator _realmValidator = new DefaultRealmValidator();

    /// <summary>
    /// ScopeManager is an instance to manage scopes.
    /// </summary>
    public ScopeManager ScopeManager { get; set; } = new();

    /// <summary>
    /// CustomHeaders is for users to customize headers
    /// </summary>
    public ConcurrentDictionary<string, List<string>> CustomHeaders { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// SetUserAgent is to set customized user agent per user requests.
    /// </summary>
    /// <param name="userAgent"></param>
    public void SetUserAgent(string userAgent)
    {
        if (CustomHeaders.TryGetValue("User-Agent", out var userAgents))
        {
            userAgents.Add(userAgent);
        }
        else
        {
            CustomHeaders["User-Agent"] = [userAgent];
        }
    }

    /// <summary>
    /// Asynchronously resolves the credential for the specified registry host.
    /// </summary>
    /// <param name="registry">The registry hostname to retrieve credentials for.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the resolved
    /// credential. If <see cref="CredentialProvider"/> is null, an empty credential is returned.
    /// </returns>
    public Task<Credential> ResolveCredentialAsync(
        string registry,
        CancellationToken cancellationToken = default)
        => CredentialProvider == null
            ? Task.FromResult(CredentialExtensions.EmptyCredential)
            : CredentialProvider.ResolveCredentialAsync(registry, cancellationToken);

    /// <summary>
    /// SendAsync sends an HTTP request asynchronously, attempting to resolve authentication if
    /// 'Authorization' header is not set.
    /// 
    /// This method intercepts the HTTP request to handle authentication challenges, including
    /// Basic and Bearer schemes. It attempts to retrieve cached tokens for the request's host
    /// and applies them to the request headers. If the server responds with an unauthorized
    /// status, it parses the authentication challenge and fetches new tokens as needed,
    /// updating the cache accordingly.
    /// </summary>
    /// <param name="originalRequest">The original HTTP request message to send.</param>
    /// <param name="partitionId">
    /// Optional cache partition identifier. When provided, tokens are isolated by this ID,
    /// enabling multi-partition scenarios where different credentials are used for the same
    /// registry.
    /// </param>
    /// <param name="allowAutoRedirect">
    /// Whether to follow redirects automatically. Set to false to capture redirect URLs
    /// (e.g., for blob locations).
    /// </param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the HTTP
    /// response message.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if the request URI is null.</exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown if required parameters (e.g., "realm") are missing in the authentication challenge.
    /// </exception>
    /// <exception cref="AuthenticationException">
    /// Thrown when the realm URL is invalid or not allowed by
    /// <see cref="RealmValidator"/>.
    /// </exception>
    public Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage originalRequest,
        string? partitionId = null,
        bool allowAutoRedirect = true,
        CancellationToken cancellationToken = default)
        => SendCoreAsync(originalRequest, partitionId, allowAutoRedirect, cancellationToken);

    /// <summary>
    /// Fetches the Basic Authentication token for the specified registry host.
    /// </summary>
    /// <param name="registry">The registry host (e.g., "docker.io").</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A Base64-encoded string representing the Basic Authentication token in the format
    /// "username:password".
    /// </returns>
    /// <exception cref="AuthenticationException">
    /// Thrown when credentials are missing or when the username or password is null or empty.
    /// </exception>
    internal async Task<string> FetchBasicAuthAsync(
        string registry,
        CancellationToken cancellationToken = default)
    {
        var credential = await ResolveCredentialAsync(registry, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(credential.Username) ||
            string.IsNullOrWhiteSpace(credential.Password))
        {
            throw new AuthenticationException(
                "Missing username or password for basic authentication.");
        }

        return Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{credential.Username}:{credential.Password}"));
    }

    /// <summary>
    /// Fetches a bearer access token for accessing a registry.
    ///
    /// When an <see cref="AccessTokenProvider"/> is configured it is
    /// consulted first.  If it returns a non-whitespace token, that
    /// token is used directly — bypassing credential resolution
    /// entirely.  Otherwise, the existing credential-based flow is
    /// used: if credential is empty or refreshToken is not set and
    /// OAuth2 authentication is not forced, the method would fetch
    /// anonymous token with a Http Get request otherwise, it would
    /// fetch OAuth2 token with a Http Post request. An unsuccessful GET may be retried with a
    /// password-grant POST when <see cref="AttemptOAuth2Fallback"/> is enabled.
    /// </summary>
    /// <param name="registry">The registry host (e.g., "docker.io").</param>
    /// <param name="realm">The authentication realm to use.</param>
    /// <param name="service">
    /// The service name for which the token is requested.
    /// </param>
    /// <param name="scopes">
    /// The scopes of access requested for the token.
    /// </param>
    /// <param name="forceRefresh">
    /// When <see langword="true"/>, instructs the provider to bypass
    /// any cached token and acquire a fresh one. This is always set
    /// after a 401 response indicates the current token is stale.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task
    /// result contains the bearer authentication token as a string.
    /// </returns>
    /// <exception cref="AuthenticationException">
    /// Thrown when credentials are missing or invalid.
    /// </exception>
    internal async Task<string> FetchBearerAuthAsync(
        string registry,
        string realm,
        string service,
        IList<string> scopes,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        // When an AccessTokenProvider is configured, try it first.
        // It returns a ready-to-use scoped token without exposing raw
        // credentials in-process.
        if (AccessTokenProvider != null)
        {
            var readOnlyScopes = scopes as IReadOnlyList<string>
                ?? (IReadOnlyList<string>)scopes.ToArray();
            var accessToken =
                await AccessTokenProvider.ResolveAccessTokenAsync(
                    registry,
                    realm,
                    service,
                    readOnlyScopes,
                    forceRefresh,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                return accessToken;
            }
        }

        var credential = await ResolveCredentialAsync(registry, cancellationToken)
            .ConfigureAwait(false);
        if (!string.IsNullOrEmpty(credential.AccessToken))
        {
            return credential.AccessToken;
        }
        if (credential.IsEmpty() ||
            (string.IsNullOrWhiteSpace(credential.RefreshToken) && !ForceAttemptOAuth2))
        {
            try
            {
                return await FetchDistributionTokenAsync(
                    realm,
                    service,
                    scopes,
                    credential.Username,
                    credential.Password,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (ResponseException error) when (
                AttemptOAuth2Fallback && error.StatusCode is not null && (int)error.StatusCode >= 400 &&
                !string.IsNullOrWhiteSpace(credential.Username) &&
                !string.IsNullOrWhiteSpace(credential.Password) &&
                !cancellationToken.IsCancellationRequested)
            {
                // Retry only a server rejection, never a transport or realm-policy failure.
            }
        }

        return await FetchOauth2TokenAsync(
            realm,
            service,
            scopes,
            credential,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// FetchDistributionToken fetches a distribution access token from the specified authentication realm with a HTTP Get request.
    /// It fetches anonymous tokens if no credential is provided.
    ///
    /// </summary>
    /// <param name="realm">The authentication realm URL.</param>
    /// <param name="service">The service name for which the token is requested.</param>
    /// <param name="scopes">A collection of scopes defining the access permissions.</param>
    /// <param name="username">The username for basic authentication (optional).</param>
    /// <param name="password">The password for basic authentication (optional).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the fetched token as a string.
    /// </returns>
    /// <exception cref="AuthenticationException">
    /// Thrown when both "access_token" and "token" are missing or empty in the response.
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// Thrown when the HTTP request fails or the response status code is not OK.
    /// </exception>
    internal async Task<string> FetchDistributionTokenAsync(
        string realm,
        string service,
        IList<string> scopes,
        string? username,
        string? password,
        CancellationToken cancellationToken = default
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, realm);
        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }

        var queryParameters = new List<KeyValuePair<string, string>>();
        if (!string.IsNullOrWhiteSpace(service))
        {
            queryParameters.Add(new KeyValuePair<string, string>("service", service));
        }

        foreach (var scope in scopes)
        {
            queryParameters.Add(new KeyValuePair<string, string>("scope", scope));
        }

        var queryString = string.Join("&", queryParameters.Select(param => $"{Uri.EscapeDataString(param.Key)}={Uri.EscapeDataString(param.Value)}"));
        var uriBuilder = new UriBuilder(request.RequestUri!)
        {
            Query = queryString
        };
        request.RequestUri = uriBuilder.Uri;

        using var response = await (TokenClient ?? BaseClient).SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw await response.ParseErrorResponseAsync(cancellationToken).ConfigureAwait(false);
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        if (root.TryGetProperty("access_token", out var accessToken) && !string.IsNullOrWhiteSpace(accessToken.GetString()))
        {
            return accessToken.GetString()!;
        }

        if (root.TryGetProperty("token", out var token) && !string.IsNullOrWhiteSpace(token.GetString()))
        {
            return token.GetString()!;
        }

        throw new AuthenticationException("Both AccessToken and Token are empty or missing");
    }

    /// <summary>
    /// Fetches an OAuth2 access token from the specified authentication realm.
    /// </summary>
    /// <param name="realm">The authentication realm URL to send the token request to.</param>
    /// <param name="service">The service identifier for which the token is requested.</param>
    /// <param name="scopes">A list of scopes to request for the token.</param>
    /// <param name="credential">The credentials containing either a refresh token or username and password.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the fetched OAuth2 access token as a string.</returns>
    /// <exception cref="AuthenticationException">
    /// Thrown when the credentials are missing or invalid, or if the access token is empty or missing.
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// Thrown when there is an issue with the HTTP request or response.
    /// </exception>
    internal async Task<string> FetchOauth2TokenAsync(
        string realm,
        string service,
        IList<string> scopes,
        Credential credential,
        CancellationToken cancellationToken = default
    )
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = string.IsNullOrEmpty(ClientId) ? _defaultClientId : ClientId
        };

        if (!string.IsNullOrWhiteSpace(service))
        {
            form["service"] = service;
        }

        if (!string.IsNullOrEmpty(credential.RefreshToken))
        {
            form["grant_type"] = "refresh_token";
            form["refresh_token"] = credential.RefreshToken;
        }
        else if (!string.IsNullOrEmpty(credential.Username) && !string.IsNullOrEmpty(credential.Password))
        {
            form["grant_type"] = "password";
            form["username"] = credential.Username;
            form["password"] = credential.Password;
        }
        else
        {
            throw new AuthenticationException("missing username or password for bearer auth");
        }

        if (scopes.Count > 0)
        {
            form["scope"] = string.Join(" ", scopes);
        }
        using var content = new FormUrlEncodedContent(form);
        using var request = new HttpRequestMessage(HttpMethod.Post, realm);
        request.Content = content;
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.FormUrlEncoded);

        using var response = await (TokenClient ?? BaseClient).SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw await response.ParseErrorResponseAsync(cancellationToken).ConfigureAwait(false);
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;
        if (root.TryGetProperty("access_token", out var accessToken) && !string.IsNullOrEmpty(accessToken.ToString()))
        {
            return accessToken.ToString();
        }

        throw new AuthenticationException("AccessToken is empty or missing");
    }

    /// <summary>
    /// Core implementation for sending HTTP requests with authentication and optional redirect control.
    /// Prepares the request, attaches any cached Basic/Bearer token, and sends the initial attempt;
    /// a 401 is delegated to <see cref="HandleUnauthorizedResponseAsync"/> for standard challenge
    /// resolution (Basic and Bearer) and the credential-free cold fallback.
    /// </summary>
    /// <param name="originalRequest">The original HTTP request message to send.</param>
    /// <param name="partitionId">Optional cache partition identifier for multi-partition isolation.</param>
    /// <param name="allowAutoRedirect">Whether to allow automatic redirect following.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the HTTP response message.
    /// </returns>
    private async Task<HttpResponseMessage> SendCoreAsync(
        HttpRequestMessage originalRequest,
        string? partitionId,
        bool allowAutoRedirect,
        CancellationToken cancellationToken)
    {
        foreach (var (headerName, headerValues) in CustomHeaders)
        {
            originalRequest.Headers.TryAddWithoutValidation(headerName, headerValues);
        }

        originalRequest.AddDefaultUserAgent();
        // Fast path: the Authorization value (Basic or Bearer) is already known ahead of time — either
        // set on this request or as a default header on the underlying HttpClient. Send it as-is and
        // skip all token acquisition, scope merging, and cache lookups.
        if (originalRequest.Headers.Authorization != null
            || BaseClient.DefaultRequestHeaders.Authorization != null
            || (!allowAutoRedirect
                && NoRedirectClient.DefaultRequestHeaders.Authorization != null))
        {
            return await SendRequestAsync(originalRequest, allowAutoRedirect, cancellationToken).ConfigureAwait(false);
        }

        // Buffer non-seekable content upfront so auth retries can
        // rewind it.  Skips when Content-Length exceeds 4 MB (to
        // avoid consuming large non-seekable streams).  Unknown-
        // length content is buffered; if it turns out to exceed
        // the cap AND is non-seekable, throws immediately.
        await BufferNonSeekableContentAsync(
            originalRequest, cancellationToken)
            .ConfigureAwait(false);

        var host = originalRequest.RequestUri?.Authority ??
                    throw new ArgumentException("originalRequest.RequestUri or originalRequest.RequestUri.Authority property is null.", nameof(originalRequest));
        var initialRequest = await originalRequest.CloneAsync(
            rewindContent: false,
            cancellationToken).ConfigureAwait(false);
        var attemptedKey = string.Empty;
        var attachedCachedBearerToken = false;

        // attempt to send request with cached auth token
        if (Cache.TryGetScheme(host, out var schemeFromCache, partitionId))
        {
            switch (schemeFromCache)
            {
                case ChallengeScheme.Basic:
                    {
                        if (Cache.TryGetToken(host, schemeFromCache, string.Empty, out var basicToken, partitionId))
                        {
                            initialRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicToken);
                            // Not flagged as a recoverable cached token: a credential-free re-derive can't
                            // help Basic auth (the same credentials are simply re-sent). Recovery targets
                            // stale Bearer tokens.
                        }

                        break;
                    }
                case ChallengeScheme.Bearer:
                    {
                        var scopes = ScopeManager.GetScopesStringForHost(host, partitionId);
                        attemptedKey = string.Join(" ", scopes);
                        if (Cache.TryGetToken(host, schemeFromCache, attemptedKey, out var bearerToken, partitionId))
                        {
                            initialRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
                            attachedCachedBearerToken = true;
                        }
                        break;
                    }
            }
        }

        var initialResponse = await SendRequestAsync(
            initialRequest,
            allowAutoRedirect,
            cancellationToken).ConfigureAwait(false);
        if (initialResponse.StatusCode != HttpStatusCode.Unauthorized)
        {
            return initialResponse;
        }

        return await HandleUnauthorizedResponseAsync(
            originalRequest: originalRequest,
            unauthorizedResponse: initialResponse,
            host: host,
            partitionId: partitionId,
            attemptedKey: attemptedKey,
            attachedCachedBearerToken: attachedCachedBearerToken,
            allowAutoRedirect: allowAutoRedirect,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles the complete 401 challenge flow, including one credential-free cold fallback when a
    /// registry returns an unusable challenge for an SDK-cached bearer token.
    /// </summary>
    private async Task<HttpResponseMessage> HandleUnauthorizedResponseAsync(
        HttpRequestMessage originalRequest,
        HttpResponseMessage unauthorizedResponse,
        string host,
        string? partitionId,
        string attemptedKey,
        bool attachedCachedBearerToken,
        bool allowAutoRedirect,
        CancellationToken cancellationToken)
    {
        // ResolveStandardChallengeAsync owns the 401's disposal: for a usable Basic/Bearer challenge it
        // disposes the response up front, before any token round-trip, so every throwing path has already
        // released it and no guard is needed here. It reports a structurally unusable challenge as a
        // result rather than throwing, but still throws for acquisition failures other than a bearer 401
        // (those propagate out, never eligible for cold recovery). A no-usable-scheme outcome leaves the
        // 401 undisposed for the caller to return or cold-probe.
        var resolution = await ResolveStandardChallengeAsync(
            originalRequest: originalRequest,
            unauthorizedResponse: unauthorizedResponse,
            host: host,
            partitionId: partitionId,
            attemptedKey: attemptedKey,
            allowAutoRedirect: allowAutoRedirect,
            cancellationToken: cancellationToken,
            backfillAttemptedScopeKey: false).ConfigureAwait(false);
        if (resolution.ResolvedResponse != null)
        {
            // ResolveStandardChallengeAsync disposed the original 401 before the token round-trip.
            return resolution.ResolvedResponse;
        }

        // Normal expired-token challenge handling above applies to every HTTP method. The cold fallback
        // is deliberately narrower: replay only the GET/HEAD methods used by OCI pull operations so a
        // credential-free probe cannot repeat a mutating POST, PUT, PATCH, or DELETE.
        if (attachedCachedBearerToken
            && (originalRequest.Method == HttpMethod.Get
                || originalRequest.Method == HttpMethod.Head))
        {
            unauthorizedResponse.Dispose();

            var coldResponse = await ProbeWithoutAuthorizationAsync(
                originalRequest: originalRequest,
                allowRegistryRedirects: allowAutoRedirect,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (coldResponse.StatusCode != HttpStatusCode.Unauthorized)
            {
                return coldResponse;
            }

            var coldResolution = await ResolveStandardChallengeAsync(
                originalRequest: originalRequest,
                unauthorizedResponse: coldResponse,
                host: host,
                partitionId: partitionId,
                attemptedKey: attemptedKey,
                allowAutoRedirect: allowAutoRedirect,
                cancellationToken: cancellationToken,
                backfillAttemptedScopeKey: true).ConfigureAwait(false);
            if (coldResolution.ResolvedResponse != null)
            {
                return coldResolution.ResolvedResponse;
            }

            return HandleUnusableChallenge(coldResponse, coldResolution);
        }

        // Cold challenge recovery does not apply: preserve the original behavior for this 401.
        return HandleUnusableChallenge(unauthorizedResponse, resolution);
    }

    /// <summary>
    /// Runs the standard 401 challenge resolution: parse the <c>WWW-Authenticate</c> challenge and,
    /// for a recognized scheme, fetch/replay credentials. Returns a resolved response on success; a
    /// terminal result carrying the exception to surface for a denied challenge or a bearer-acquisition
    /// 401 (captured with its original stack); or a no-usable-scheme result when the challenge is
    /// malformed or names no recognized scheme. Acquisition failures other than a 401 still throw.
    /// </summary>
    /// <remarks>
    /// Disposal: for a recognized scheme (Basic or Bearer) this method disposes
    /// <paramref name="unauthorizedResponse"/> up front, before the credential/token round-trip, matching
    /// the client's long-standing eager-dispose behavior, whether the outcome is resolved or terminal.
    /// A no-usable-scheme outcome leaves the response undisposed, so the caller can return it or hand it
    /// to the fallback.
    /// <para>
    /// When <paramref name="backfillAttemptedScopeKey"/> is <c>true</c> (the recovery re-derive, gated on
    /// a stale token having been attached), a token obtained from a successful (2xx/3xx) response is also
    /// cached under <paramref name="attemptedKey"/> so the stale token that provoked recovery is replaced
    /// and future requests under that scope key don't re-enter recovery.
    /// </para>
    /// </remarks>
    private async Task<StandardAuthResult> ResolveStandardChallengeAsync(
        HttpRequestMessage originalRequest,
        HttpResponseMessage unauthorizedResponse,
        string host,
        string? partitionId,
        string attemptedKey,
        bool allowAutoRedirect,
        CancellationToken cancellationToken,
        bool backfillAttemptedScopeKey = false)
    {
        if (!Challenge.TryParse(
                unauthorizedResponse.Headers.WwwAuthenticate.FirstOrDefault()?.ToString(),
                out var parsedChallenge))
        {
            // A malformed WWW-Authenticate header names no usable scheme: leave the 401 undisposed so an
            // eligible stale-token GET/HEAD can still cold-probe and an ineligible caller gets the 401 back.
            return StandardAuthResult.NoUsableScheme();
        }

        var (schemeFromChallenge, parameters) = parsedChallenge;

        // Attempt again with credentials for recognized schemes
        switch (schemeFromChallenge)
        {
            case ChallengeScheme.Basic:
                {
                    // Usable challenge: release the original 401 before the credential round-trip.
                    unauthorizedResponse.Dispose();
                    var basicAuthToken = await FetchBasicAuthAsync(
                        registry: host,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                    Cache.SetCache(
                        registry: host,
                        scheme: schemeFromChallenge,
                        scopeKey: string.Empty,
                        token: basicAuthToken,
                        partitionId: partitionId);

                    // Attempt again with basic token
                    var basicRequest = await originalRequest.CloneAsync(
                        rewindContent: true,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                    basicRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuthToken);
                    var basicResponse = await SendRequestAsync(
                        request: basicRequest,
                        allowAutoRedirect: allowAutoRedirect,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                    return StandardAuthResult.Resolved(response: basicResponse);
                }
            case ChallengeScheme.Bearer:
                {
                    // Usable recognized scheme: release the 401 before the token round-trip.
                    unauthorizedResponse.Dispose();

                    if (parameters == null)
                    {
                        return StandardAuthResult.Terminal(ExceptionDispatchInfo.Capture(
                            new AuthenticationException(
                                "Missing parameters in the Www-Authenticate challenge.")));
                    }

                    var existingScopes = ScopeManager.GetScopesForHost(
                        registry: host,
                        partitionId: partitionId);
                    var mergedScopes = MergeChallengeScopes(
                        existingScopes: existingScopes,
                        scopesString: parameters.TryGetValue("scope", out var scopesString)
                            ? scopesString
                            : null);

                    StandardAuthResult ResolvedWithKeyBackfill(HttpResponseMessage response, string token)
                    {
                        // Backfill the stale lookup key only after a recovered, cacheable token succeeds;
                        // never populate the empty catch-all key or cache a token from opaque scopes.
                        // Dispose the just-received response if that post-send backfill throws (e.g. a
                        // custom ICache.SetCache) so recovery never leaks it.
                        try
                        {
                            if (backfillAttemptedScopeKey
                                && !string.IsNullOrEmpty(attemptedKey)
                                && IsSuccessOrRedirect(response.StatusCode)
                                && !mergedScopes.HasOpaqueScopes
                                && !string.Equals(
                                    mergedScopes.CacheKey,
                                    attemptedKey,
                                    StringComparison.Ordinal))
                            {
                                Cache.SetCache(
                                    registry: host,
                                    scheme: ChallengeScheme.Bearer,
                                    scopeKey: attemptedKey,
                                    token: token,
                                    partitionId: partitionId);
                            }
                        }
                        catch
                        {
                            response.Dispose();
                            throw;
                        }
                        return StandardAuthResult.Resolved(response);
                    }

                    // Attempt to send request when the scope changes and a token cache hits
                    if (!mergedScopes.HasOpaqueScopes
                        && mergedScopes.CacheKey != attemptedKey
                        && Cache.TryGetToken(
                            host,
                            schemeFromChallenge,
                            mergedScopes.CacheKey,
                            out var cachedToken,
                            partitionId))
                    {
                        var cachedTokenRequest = await originalRequest.CloneAsync(
                            rewindContent: true,
                            cancellationToken: cancellationToken).ConfigureAwait(false);
                        cachedTokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cachedToken);
                        var cachedTokenResponse = await SendRequestAsync(
                            request: cachedTokenRequest,
                            allowAutoRedirect: allowAutoRedirect,
                            cancellationToken: cancellationToken).ConfigureAwait(false);

                        if (cachedTokenResponse.StatusCode != HttpStatusCode.Unauthorized)
                        {
                            return ResolvedWithKeyBackfill(cachedTokenResponse, cachedToken);
                        }
                        cachedTokenResponse.Dispose();
                    }

                    if (!parameters.TryGetValue("realm", out var realm))
                    {
                        // 'realm' is required as it specifies the token endpoint URL for Bearer auth.
                        return StandardAuthResult.Terminal(ExceptionDispatchInfo.Capture(
                            new KeyNotFoundException(
                                "Missing 'realm' parameter in WWW-Authenticate Bearer challenge.")));
                    }

                    // Validate realm URL before sending credentials.
                    if (!Uri.TryCreate(
                            realm, UriKind.Absolute,
                            out var realmUri)
                        || string.IsNullOrEmpty(realmUri.Host))
                    {
                        return StandardAuthResult.Terminal(ExceptionDispatchInfo.Capture(
                            new AuthenticationException($"Invalid realm URL: '{realm}'")));
                    }

                    var registryUri = originalRequest.RequestUri!;
                    if (!await RealmValidator.IsRealmAllowedAsync(registryUri, realmUri, cancellationToken).ConfigureAwait(false))
                    {
                        return StandardAuthResult.Terminal(ExceptionDispatchInfo.Capture(
                            new AuthenticationException(
                                $"Authentication realm '{realmUri}' is not allowed for registry '{host}'.")));
                    }

                    if (!parameters.TryGetValue("service", out var service))
                    {
                        // some registries may omit the `service` parameter; use an empty string when absent.
                        service = string.Empty;
                    }

                    // Try to fetch bearer token based on the challenge header
                    string bearerAuthToken;
                    try
                    {
                        bearerAuthToken = await FetchBearerAuthAsync(
                            registry: host,
                            realm: realm,
                            service: service,
                            scopes: mergedScopes.TokenRequestScopes,
                            forceRefresh: true,
                            cancellationToken: cancellationToken).ConfigureAwait(false);
                    }
                    catch (ResponseException e) when (e.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        // A bearer provider or token endpoint returned 401. Report it as terminal so a
                        // stale cached GET/HEAD can cold-probe once; terminal handling rethrows this
                        // original public exception with its stack intact.
                        return StandardAuthResult.Terminal(ExceptionDispatchInfo.Capture(e));
                    }
                    if (!mergedScopes.HasOpaqueScopes)
                    {
                        Cache.SetCache(
                            registry: host,
                            scheme: schemeFromChallenge,
                            scopeKey: mergedScopes.CacheKey,
                            token: bearerAuthToken,
                            partitionId: partitionId);
                    }

                    var bearerRequest = await originalRequest.CloneAsync(
                        rewindContent: true,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                    bearerRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerAuthToken);
                    var bearerResponse = await SendRequestAsync(
                        request: bearerRequest,
                        allowAutoRedirect: allowAutoRedirect,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                    return ResolvedWithKeyBackfill(bearerResponse, bearerAuthToken);
                }
            default:
                return StandardAuthResult.NoUsableScheme();
        }
    }

    private static MergedScopes MergeChallengeScopes(
        SortedSet<Scope> existingScopes,
        string? scopesString)
    {
        var structuredScopes = existingScopes;
        var copiedStructuredScopes = false;

        // Opaque (unparseable) challenge scopes are preserved verbatim, including
        // duplicates and original order. The opaque list is allocated lazily so
        // the common structured-only case doesn't allocate it.
        List<string>? opaqueScopes = null;

        if (!string.IsNullOrWhiteSpace(scopesString))
        {
            var remainingScopes = scopesString.AsSpan();
            while (TryReadNextScope(ref remainingScopes, out var scopeSpan))
            {
                if (Scope.TryParse(scopeSpan, out var scope))
                {
                    if (!copiedStructuredScopes)
                    {
                        // Copy before merging so an attacker-controllable challenge scope
                        // cannot mutate the client's persisted scopes for this host.
                        structuredScopes = new SortedSet<Scope>(existingScopes);
                        copiedStructuredScopes = true;
                    }

                    Scope.AddOrMergeScopeCopyOnWrite(structuredScopes, scope);
                }
                else
                {
                    (opaqueScopes ??= new()).Add(scopeSpan.ToString());
                }
            }
        }

        var tokenRequestScopes = BuildTokenRequestScopes(
            structuredScopes,
            opaqueScopes,
            out var cacheKey);
        return new MergedScopes(
            tokenRequestScopes,
            cacheKey,
            hasOpaqueScopes: opaqueScopes != null);
    }

    private static List<string> BuildTokenRequestScopes(
        SortedSet<Scope> structuredScopes,
        List<string>? opaqueScopes,
        out string cacheKey)
    {
        var opaqueScopeCount = opaqueScopes?.Count ?? 0;
        var tokenRequestScopes =
            new List<string>(structuredScopes.Count + opaqueScopeCount);

        foreach (var scope in structuredScopes)
        {
            tokenRequestScopes.Add(scope.ToString());
        }

        // The cache key is derived from structured scopes only and must match the
        // single-space canonicalization used for the cached-token lookup. When opaque
        // scopes are present the token is not cached, so the key is left empty. At this
        // point tokenRequestScopes holds only the structured scopes, so it can be joined
        // directly before the opaque scopes are appended.
        cacheKey = opaqueScopeCount > 0
            ? string.Empty
            : string.Join(' ', tokenRequestScopes);

        if (opaqueScopes != null)
        {
            tokenRequestScopes.AddRange(opaqueScopes);
        }

        return tokenRequestScopes;
    }

    private static bool TryReadNextScope(
        ref ReadOnlySpan<char> remainingScopes,
        out ReadOnlySpan<char> scope)
    {
        while (!remainingScopes.IsEmpty)
        {
            var separatorIndex = remainingScopes.IndexOf(' ');
            scope = separatorIndex < 0
                ? remainingScopes
                : remainingScopes[..separatorIndex];
            remainingScopes = separatorIndex < 0
                ? []
                : remainingScopes[(separatorIndex + 1)..];

            if (!scope.IsEmpty)
            {
                return true;
            }
        }

        scope = [];
        return false;
    }

    private readonly struct MergedScopes
    {
        public MergedScopes(
            List<string> tokenRequestScopes,
            string cacheKey,
            bool hasOpaqueScopes)
        {
            TokenRequestScopes = tokenRequestScopes;
            CacheKey = cacheKey;
            HasOpaqueScopes = hasOpaqueScopes;
        }

        public List<string> TokenRequestScopes { get; }

        public string CacheKey { get; }

        public bool HasOpaqueScopes { get; }
    }

    /// <summary>
    /// Sends a fresh, bodyless copy of an OCI GET or HEAD request without authorization to elicit the
    /// challenge that the registry serves to a cold client.
    /// </summary>
    private async Task<HttpResponseMessage> ProbeWithoutAuthorizationAsync(
        HttpRequestMessage originalRequest,
        bool allowRegistryRedirects,
        CancellationToken cancellationToken)
    {
        var probe = new HttpRequestMessage(
            originalRequest.Method,
            originalRequest.RequestUri)
        {
            Version = originalRequest.Version,
            VersionPolicy = originalRequest.VersionPolicy,
        };
        foreach (var option in originalRequest.Options)
        {
            probe.Options.TryAdd(option.Key, option.Value);
        }
        foreach (var header in originalRequest.Headers)
        {
            if (!string.Equals(
                    header.Key,
                    _authorizationHeader,
                    StringComparison.OrdinalIgnoreCase))
            {
                probe.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return await SendRequestAsync(
            probe,
            allowRegistryRedirects,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Whether <paramref name="statusCode"/> is a 2xx success or a 3xx redirect (200–399).</summary>
    private static bool IsSuccessOrRedirect(HttpStatusCode statusCode)
        => (int)statusCode is >= 200 and < 400;

    /// <summary>
    /// Applies the terminal outcome of an unusable challenge: rethrow the captured error, or return the
    /// 401 unchanged when it named no recognized authentication scheme.
    /// </summary>
    private static HttpResponseMessage HandleUnusableChallenge(
        HttpResponseMessage unauthorizedResponse, StandardAuthResult resolution)
    {
        if (resolution.TerminalError != null)
        {
            // The 401 was already disposed by ResolveStandardChallengeAsync before the token
            // round-trip; dispose is idempotent. Surface the exact public exception and its stack.
            unauthorizedResponse.Dispose();
            resolution.TerminalError.Throw();
        }

        // No recognized challenge scheme: hand the 401 back to the caller.
        return unauthorizedResponse;
    }

    /// <summary>
    /// The outcome of <see cref="ResolveStandardChallengeAsync"/>: a resolved response, a terminal error
    /// to surface, or neither — meaning the 401 named no usable scheme and should be returned as-is.
    /// </summary>
    private sealed class StandardAuthResult
    {
        private StandardAuthResult(
            HttpResponseMessage? resolvedResponse,
            ExceptionDispatchInfo? terminalError)
        {
            ResolvedResponse = resolvedResponse;
            TerminalError = terminalError;
        }

        /// <summary>The resolved response, or <c>null</c> when the challenge was not resolved.</summary>
        public HttpResponseMessage? ResolvedResponse { get; }

        /// <summary>
        /// The exception to rethrow for a terminal challenge (malformed or denied realm, or a
        /// bearer-acquisition 401); <c>null</c> when the challenge resolved or named no usable scheme.
        /// </summary>
        public ExceptionDispatchInfo? TerminalError { get; }

        /// <summary>A challenge resolved by fetching or replaying credentials.</summary>
        public static StandardAuthResult Resolved(HttpResponseMessage response)
            => new(resolvedResponse: response, terminalError: null);

        /// <summary>
        /// A recognized challenge that failed terminally. The captured exception is rethrown verbatim,
        /// with its original stack, once any recovery has been exhausted.
        /// </summary>
        public static StandardAuthResult Terminal(ExceptionDispatchInfo error)
            => new(resolvedResponse: null, terminalError: error);

        /// <summary>
        /// The 401 named no recognized authentication scheme; the caller returns the response unchanged.
        /// </summary>
        public static StandardAuthResult NoUsableScheme()
            => new(resolvedResponse: null, terminalError: null);
    }

    /// <summary>
    /// Sends an HTTP request using the configured HttpClient with optional redirect control.
    /// HttpCompletionOption.ResponseHeadersRead is used here to enable content streaming and to 
    /// avoid buffering the entire response body in memory.
    /// </summary>
    /// <param name="request">The HTTP request message to send.</param>
    /// <param name="allowAutoRedirect">Whether to allow automatic redirect following. Default is true.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The HTTP response message.</returns>
    private async Task<HttpResponseMessage> SendRequestAsync(
        HttpRequestMessage request,
        bool allowAutoRedirect = true,
        CancellationToken cancellationToken = default)
    {
        var client = allowAutoRedirect ? BaseClient : NoRedirectClient;
        return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Buffers non-seekable request content in place so that auth
    /// retries can rewind the body.  Uses the built-in
    /// <see cref="HttpContent.LoadIntoBufferAsync(long)"/> which
    /// handles both known and unknown Content-Length, enforces the
    /// size cap, and makes the stream seekable after completion.
    /// Seekable content that exceeds the buffer size is left
    /// untouched (it can be rewound directly without buffering).
    /// Non-seekable content with a declared Content-Length over the
    /// limit is also left untouched — the first send proceeds but
    /// a 401 retry would fail because the body cannot be replayed.
    /// <see cref="ByteArrayContent"/> is left untouched because it
    /// already replays from its own array.
    /// </summary>
    /// <remarks>
    /// <see cref="HttpContent.LoadIntoBufferAsync(long)"/> on
    /// .NET 8 does not accept a <see cref="CancellationToken"/>;
    /// the token is only used in the catch-path stream check.
    /// With the 4 MB cap, buffering completes quickly in practice.
    /// </remarks>
    private static async Task BufferNonSeekableContentAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var content = request.Content;
        // Byte array content (including StringContent and FormUrlEncodedContent) already
        // replays from its own array, so buffering it would only copy the payload. Chunked
        // blob uploads send one such request per chunk.
        if (content is null or ByteArrayContent)
        {
            return;
        }

        // If Content-Length is declared and exceeds the cap, skip
        // buffering to avoid partially consuming a non-seekable
        // stream.  Seekable streams will still work (rewind on
        // retry); non-seekable ones accept the limitation.
        if (content.Headers.ContentLength > MaxBufferSize)
        {
            return;
        }

        try
        {
            await content.LoadIntoBufferAsync(MaxBufferSize)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            // Content exceeds MaxBufferSize (unknown length that
            // turned out to be too large).  If the stream is
            // seekable, rewind and continue — the retry path can
            // replay the body directly.  If not seekable, the
            // stream is partially consumed and unrecoverable;
            // rethrow so the caller gets a clear size error rather
            // than a confusing "stream already consumed" later.
            var stream = await content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            if (!stream.CanSeek)
            {
                throw;
            }

            stream.Position = 0;
        }
    }
}
