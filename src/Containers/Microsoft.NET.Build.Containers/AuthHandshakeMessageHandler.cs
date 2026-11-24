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
    private readonly bool _isInsecureRegistry;
    private readonly ILogger _logger;
    private readonly RegistryMode _registryMode;
    private static ConcurrentDictionary<string, AuthenticationHeaderValue?> _authenticationHeaders = new();

    /// <summary>
    /// IPv4 ranges considered unsafe to send token requests to. Loopback (127.0.0.0/8) is
    /// covered by <see cref="IPAddress.IsLoopback(IPAddress)"/>.
    /// </summary>
    private static readonly IPNetwork[] BlockedV4Networks =
    [
        IPNetwork.Parse("0.0.0.0/8"),      // "this network" / unspecified
        IPNetwork.Parse("10.0.0.0/8"),     // private (RFC 1918)
        IPNetwork.Parse("172.16.0.0/12"),  // private (RFC 1918)
        IPNetwork.Parse("192.168.0.0/16"), // private (RFC 1918)
        IPNetwork.Parse("169.254.0.0/16"), // link-local
        IPNetwork.Parse("224.0.0.0/24"),   // link-local multicast
    ];

    public AuthHandshakeMessageHandler(string registryName, bool isInsecureRegistry, HttpMessageHandler innerHandler, ILogger logger, RegistryMode mode) : base(innerHandler)
    {
        _registryName = registryName;
        _isInsecureRegistry = isInsecureRegistry;
        _logger = logger;
        _registryMode = mode;
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
        // For bearer auth, validate the realm URL before any credential lookup so that a malicious or
        // compromised registry response cannot trigger credential retrieval toward a hostile token
        // endpoint, and so that validation errors aren't masked by credential errors.
        Uri? validatedBearerRealm = null;
        if (scheme.Equals(BearerAuthScheme, StringComparison.OrdinalIgnoreCase))
        {
            Debug.Assert(bearerAuthInfo is not null);
            validatedBearerRealm = ValidateRealmUri(bearerAuthInfo.Realm, registry, _isInsecureRegistry);
        }

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

            // Obtain a Bearer token, when the credentials are:
            // - an identity token: use it for OAuth
            // - a username/password: use them for Basic auth, and fall back to OAuth

            if (string.IsNullOrWhiteSpace(privateRepoCreds.IdentityToken))
            {
                var authenticationValueAndDuration = await TryTokenGetAsync(privateRepoCreds, bearerAuthInfo, validatedBearerRealm, cancellationToken).ConfigureAwait(false);
                if (authenticationValueAndDuration is not null)
                {
                    return authenticationValueAndDuration;
                }
            }

            return await TryOAuthPostAsync(privateRepoCreds, bearerAuthInfo, validatedBearerRealm, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            return null;
        }
    }

    /// <summary>
    /// Validates the bearer realm URL returned in a WWW-Authenticate challenge before the client
    /// uses it to fetch a token.
    /// Exception for legitimate insecure dev/on-prem registries that use IP-literal
    /// hostnames and return realms pointing back at themselves: an IP-literal realm host is
    /// allowed when <paramref name="isInsecureRegistry"/> is true and the realm host matches the
    /// registry host (port-independent).
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

        // IP-literal guard. We use Uri.IdnHost (the ASCII/canonical host) rather than
        // Uri.Host so that Unicode-dot forms, which the runtime canonicalizes back to
        // 127.0.0.1 when the request is actually sent, cannot bypass the check by
        // appearing as a DNS name to Uri.HostNameType.
        string realmHost = TrimTrailingDot(realmUri.IdnHost);
        if (IPAddress.TryParse(realmHost, out IPAddress? realmIp)
            && IsBlockedIpLiteral(realmIp))
        {
            // Exception: allow IP-literal realm whose host matches the registry's host
            // when the registry is insecure. This supports legitimate private/on-prem dev
            // registries (e.g. 192.168.1.5:5000) whose realm points back to themselves.
            if (!(isInsecureRegistry && RegistryHostMatchesIp(registryName, realmIp)))
            {
                throw new InvalidAuthResponseException(
                    registryName,
                    Resource.FormatString(nameof(Strings.InvalidAuthResponse_PrivateIpLiteralRealm), realm, realmHost));
            }
        }
        else if (IsLoopbackDnsName(realmHost)
            && !(isInsecureRegistry && RegistryIsLoopbackEquivalent(registryName)))
        {
            // RFC 6761 reserves "localhost" and "*.localhost" for loopback resolution, so a
            // realm host of those names carries the same risk as a literal 127.0.0.1 - the
            // runtime resolves them to loopback regardless of /etc/hosts. Apply the same
            // exception model the IP-literal guard uses (insecure + registry is also loopback-equivalent).
            throw new InvalidAuthResponseException(
                registryName,
                Resource.FormatString(nameof(Strings.InvalidAuthResponse_PrivateIpLiteralRealm), realm, realmHost));
        }

        return realmUri;
    }

    /// <summary>
    /// Returns true when <paramref name="host"/> is one of the DNS names RFC 6761 reserves
    /// for loopback resolution.
    /// Callers must pass an already-trimmed host (see <see cref="TrimTrailingDot"/>).
    /// </summary>
    private static bool IsLoopbackDnsName(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Strips a single FQDN root-zone trailing dot from <paramref name="host"/>. DNS treats
    /// "localhost" and "localhost." as equivalent, but Uri.IdnHost preserves the dot, so
    /// without this normalization a realm could bypass the IP-literal and loopback-name
    /// guards using forms like "127.0.0.1." or "localhost.".
    /// </summary>
    private static string TrimTrailingDot(string host) =>
        host.Length > 1 && host[^1] == '.' ? host[..^1] : host;

    /// <summary>
    /// Returns true when <paramref name="registryName"/> identifies the local machine via a
    /// loopback IP literal (<c>127.0.0.0</c> or <c>::1</c>) or an RFC 6761 loopback name
    /// (<c>localhost</c> / <c>*.localhost</c>).
    /// </summary>
    private static bool RegistryIsLoopbackEquivalent(string registryName)
    {
        if (!Uri.TryCreate($"https://{registryName}", UriKind.Absolute, out Uri? uri))
        {
            return false;
        }
        string host = TrimTrailingDot(uri.IdnHost);
        return IsLoopbackDnsName(host)
            || (IPAddress.TryParse(host, out IPAddress? ip) && IPAddress.IsLoopback(ip));
    }

    /// <summary>
    /// Returns true if the IP address is considered unsafe to send token requests to:
    /// loopback, link-local, private, or unspecified.
    /// </summary>
    private static bool IsBlockedIpLiteral(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }
        // IPv4-mapped IPv6: unwrap so we don't need a parallel set of IPv4-in-IPv6 CIDRs.
        if (ip.IsIPv4MappedToIPv6)
        {
            return IsBlockedIpLiteral(ip.MapToIPv4());
        }

        foreach (IPNetwork net in BlockedV4Networks)
        {
            if (net.Contains(ip))
            {
                return true;
            }
        }

        // IPv6-only properties; all return false for IPv4 so the family gate is implicit.
        // Multicast scope is read directly from the second byte.
        return ip.Equals(IPAddress.IPv6Any)
            || ip.IsIPv6LinkLocal
            || ip.IsIPv6SiteLocal
            || ip.IsIPv6UniqueLocal
            || (ip.IsIPv6Multicast && (ip.GetAddressBytes()[1] & 0x0f) == 0x02);
    }

    /// <summary>
    /// Returns true when the registry name's host portion (port stripped) refers to the same machine
    /// as <paramref name="ip"/>. Used to allow IP-literal realms whose host matches the registry host
    /// in insecure-registry scenarios.
    /// </summary>
    /// <remarks>
    /// Two forms of match are recognized: an IP-literal registry name whose address equals
    /// <paramref name="ip"/>, and a registry name of <c>localhost</c> (or any <c>*.localhost</c>
    /// subdomain) paired with any loopback IP.
    /// </remarks>
    private static bool RegistryHostMatchesIp(string registryName, IPAddress ip)
    {
        // Use Uri to handle "host[:port]" and bracketed IPv6 ("[::1]:5000") splitting.
        // The synthetic scheme is parser convenience — we never use the URL.
        if (!Uri.TryCreate($"https://{registryName}", UriKind.Absolute, out Uri? uri))
        {
            return false;
        }
        // IdnHost (vs. Host) gives us three things in one shot:
        //   1. Strips the "[" and "]" around IPv6 literals so the host feeds straight
        //      into IPAddress.TryParse.
        //   2. Canonicalizes Unicode/IDN host forms to ASCII (e.g. fullwidth-dot
        //      "127\uFF0E0\uFF0E0\uFF0E1" -> "127.0.0.1"), matching what HttpClient
        //      actually resolves.
        //   3. Matches the canonicalization ValidateRealmUri uses, so realm-vs-registry
        //      host comparisons stay consistent on both sides.
        string host = TrimTrailingDot(uri.IdnHost);

        // RFC 6761 reserves "localhost" (and "*.localhost") for loopback addresses, so a
        // localhost-named registry returning a 127.0.0.0/8 or ::1 realm is legitimate.
        if (IPAddress.IsLoopback(ip) && IsLoopbackDnsName(host))
        {
            return true;
        }

        return IPAddress.TryParse(host, out IPAddress? registryIp) && registryIp.Equals(ip);
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
    private async Task<(AuthenticationHeaderValue, DateTimeOffset)?> TryOAuthPostAsync(DockerCredentials privateRepoCreds, AuthInfo bearerAuthInfo, Uri realmUri, CancellationToken cancellationToken)
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

        using HttpResponseMessage postResponse = await base.SendAsync(postMessage, cancellationToken).ConfigureAwait(false);
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
    private async Task<(AuthenticationHeaderValue, DateTimeOffset)?> TryTokenGetAsync(DockerCredentials privateRepoCreds, AuthInfo bearerAuthInfo, Uri realmUri, CancellationToken cancellationToken)
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

        using var tokenResponse = await base.SendAsync(message, cancellationToken).ConfigureAwait(false);
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

        if (_authenticationHeaders.TryGetValue(_registryName, out AuthenticationHeaderValue? header))
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
                        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
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
