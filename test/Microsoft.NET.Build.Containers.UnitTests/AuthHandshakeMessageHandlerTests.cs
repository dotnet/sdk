// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Web;
using System.Net.Http.Headers;
using System.Collections.Specialized;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.NET.Build.Containers.UnitTests
{
    public class AuthHandshakeMessageHandlerTests
    {
        private const string TestRegistryName = "registry.test";
        private const string RequestUrl = $"https://{TestRegistryName}/v2";
        private const string BearerRealmUrl = $"https://bearer.test/token";

        [Theory]
        [InlineData("SDK_CONTAINER_REGISTRY_UNAME", "SDK_CONTAINER_REGISTRY_PWORD", (int)RegistryMode.Push)]
        [InlineData("DOTNET_CONTAINER_PUSH_REGISTRY_UNAME", "DOTNET_CONTAINER_PUSH_REGISTRY_PWORD", (int)RegistryMode.Push)]
        [InlineData("DOTNET_CONTAINER_PULL_REGISTRY_UNAME", "DOTNET_CONTAINER_PULL_REGISTRY_PWORD", (int)RegistryMode.Pull)]
        [InlineData("DOTNET_CONTAINER_PULL_REGISTRY_UNAME", "DOTNET_CONTAINER_PULL_REGISTRY_PWORD", (int)RegistryMode.PullFromOutput)]
        [InlineData("SDK_CONTAINER_REGISTRY_UNAME", "SDK_CONTAINER_REGISTRY_PWORD", (int)RegistryMode.PullFromOutput)]
        public void GetDockerCredentialsFromEnvironment_ReturnsCorrectValues(string unameVarName, string pwordVarName, int mode)
        {
            string? originalUnameValue = Environment.GetEnvironmentVariable(unameVarName);
            string? originalPwordValue = Environment.GetEnvironmentVariable(pwordVarName);

            Environment.SetEnvironmentVariable(unameVarName, "uname");
            Environment.SetEnvironmentVariable(pwordVarName, "pword");

            if (AuthHandshakeMessageHandler.GetDockerCredentialsFromEnvironment((RegistryMode)mode) is (string credU, string credP))
            {
                Assert.Equal("uname", credU);
                Assert.Equal("pword", credP);
            }
            else 
            {
                Assert.Fail("Should have parsed credentials from environment");
            }


            // restore env variable values
            Environment.SetEnvironmentVariable(unameVarName, originalUnameValue);
            Environment.SetEnvironmentVariable(pwordVarName, originalPwordValue);
        }

        [Theory]
        [MemberData(nameof(GetAuthenticateTestData))]
        public async Task Authenticate(string authConf, Func<HttpRequestMessage, HttpResponseMessage> server)
        {
            string authFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(authFile, authConf);
                Environment.SetEnvironmentVariable("REGISTRY_AUTH_FILE", authFile);

                var authHandler = new AuthHandshakeMessageHandler(TestRegistryName, isInsecureRegistry: false, new ServerMessageHandler(server), NullLogger.Instance, RegistryMode.Push);
                using var httpClient = new HttpClient(authHandler);

                var response = await httpClient.GetAsync(RequestUrl, TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
            finally
            {
                try
                {
                    File.Delete(authFile);
                }
                catch
                { }
            }
        }

        public static IEnumerable<object[]> GetAuthenticateTestData()
        {
            // Check auth with username and password.
            // The '<token>' username has a special meaning that is already handled by the docker-creds-provider library.
            // We cover it it in the test to verify the SDK doesn't handled it special.
            string password = "pass";
            string username = "user";
            foreach (string user in new[] { username, "<token>"})
            {
                // Basic auth
                yield return new object[] {
                    ConfigAuthWithUserAndPassword(user, password),
                    ServerWithBasicAuth(user, password)
                    };

                // Basic auth for token
                yield return new object[] {
                    ConfigAuthWithUserAndPassword(user, password),
                    ServerWithBasicAuthForToken($"realm=\"{BearerRealmUrl}\"", BearerRealmUrl, user, password,
                        queryParameters: new())
                    };

                // OAuth password auth
                yield return new object[] {
                    ConfigAuthWithUserAndPassword(user, password),
                    ServerWithOAuthForToken($"realm=\"{BearerRealmUrl}\"", BearerRealmUrl,
                        formParameters: new()
                        {
                            { "client_id", "netsdkcontainers" },
                            { "grant_type", "password" },
                            { "username", user },
                            { "password", password }
                        })
                    };
            }

            // Check auth with an identity token.
            string identityToken = "my-identity-token";
            yield return new object[] {
                ConfigAuthWithIdentityToken(identityToken),
                ServerWithOAuthForToken($"realm=\"{BearerRealmUrl}\"", BearerRealmUrl,
                    formParameters: new()
                    {
                        { "client_id", "netsdkcontainers" },
                        { "grant_type", "refresh_token" },
                        { "refresh_token", identityToken }
                    })
                };

            // Verify the bearer parameters (service/scope) are passed.
            // With OAuth auth as form parameters
            string scope = "my-scope";
            string service = "my-service";
            yield return new object[] {
                ConfigAuthWithIdentityToken(identityToken),
                ServerWithOAuthForToken($"realm=\"{BearerRealmUrl}\", service={service}, scope={scope}", BearerRealmUrl,
                    formParameters: new()
                    {
                        { "client_id", "netsdkcontainers" },
                        { "grant_type", "refresh_token" },
                        { "refresh_token", identityToken },
                        { "service", service },
                        { "scope", scope }
                    })
                };
            // With Basic auth as query parameters
            yield return new object[] {
                ConfigAuthWithUserAndPassword(username, password),
                ServerWithBasicAuthForToken($"realm=\"{BearerRealmUrl}\", service={service}, scope={scope}", BearerRealmUrl, username, password,
                    queryParameters: new()
                    {
                        { "service", service },
                        { "scope", scope }
                    })
                };

            static string ConfigAuthWithUserAndPassword(string username, string password) =>
            $$"""
            {
                "auths": {
                    "{{TestRegistryName}}": {
                        "auth": "{{GetUserPasswordBase64(username, password)}}"
                    }
                }
            }
            """;

            static string ConfigAuthWithIdentityToken(string identityToken) =>
            $$"""
            {
                "auths": {
                    "{{TestRegistryName}}": {
                        "identitytoken": {{identityToken}},
                        "auth": "{{GetUserPasswordBase64("__", "__")}}"
                    }
                }
            }
            """;
        }

        static string GetUserPasswordBase64(string username, string password)
            => Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));

        static Func<HttpRequestMessage, HttpResponseMessage> ServerWithBasicAuth(string username, string password)
        {
            return (HttpRequestMessage request) =>
            {
                if (request.RequestUri?.ToString() == RequestUrl &&
                    IsBasicAuthenticated(request, username, password))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }

                return CreateRequestAuthenticateResponse("Basic", "");
            };

            static bool IsBasicAuthenticated(HttpRequestMessage requestMessage, string username, string password)
            {
                AuthenticationHeaderValue? header = requestMessage.Headers.Authorization;
                if (header is null)
                {
                    return false;
                }
                return header.Scheme == "Basic" && header.Parameter == GetUserPasswordBase64(username, password);
            }
        }

        static Func<HttpRequestMessage, HttpResponseMessage> ServerWithBasicAuthForToken(string authenticateParameters, string requestUri, string username, string password, Dictionary<string, string> queryParameters)
            => ServerWithBearerAuth(authenticateParameters, requestUri, HttpMethod.Get, queryParameters, new(), new AuthenticationHeaderValue("Basic", GetUserPasswordBase64(username, password)));

        static Func<HttpRequestMessage, HttpResponseMessage> ServerWithOAuthForToken(string authenticateParameters, string requestUri, Dictionary<string, string> formParameters)
            => ServerWithBearerAuth(authenticateParameters, requestUri, HttpMethod.Post, new(), formParameters, null);

        static Func<HttpRequestMessage, HttpResponseMessage> ServerWithBearerAuth(string authenticateParameters, string requestUri, HttpMethod method, Dictionary<string, string> queryParameters, Dictionary<string, string> formParameters, AuthenticationHeaderValue? authHeader)
        {
            const string BearerToken = "my-bearer-token";

            return (HttpRequestMessage request) =>
            {
                if (request.RequestUri?.ToString() == RequestUrl &&
                    IsBearerAuthenticated(request, BearerToken))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }

                if (request.RequestUri?.ToString() == BearerRealmUrl)
                {
                    // Verify the method is the expected one.
                    Assert.Equal(method, request.Method);

                    // Verify the query parameter are the expected ones.
                    AssertParametersAreEqual(queryParameters, request.RequestUri.Query);

                    // Verify the auth header is the expected one.
                    AuthenticationHeaderValue? header = request.Headers.Authorization;
                    if (authHeader is not null)
                    {
                        Assert.NotNull(header);
                        Assert.Equal(header.Scheme, authHeader.Scheme);
                        Assert.Equal(header.Parameter, authHeader.Parameter);
                    }
                    else
                    {
                        Assert.Null(header);
                    }

                    // Verify the content.
                    string content = request.Content is null ? "" : request.Content.ReadAsStringAsync().Result;
                    AssertParametersAreEqual(formParameters, content);

                    // Issue the token.
                    return CreateBearerTokenResponse(BearerToken);
                }

                return CreateRequestAuthenticateResponse("Bearer", authenticateParameters);
            };

            static bool IsBearerAuthenticated(HttpRequestMessage requestMessage, string bearerToken)
            {
                AuthenticationHeaderValue? header = requestMessage.Headers.Authorization;
                if (header is null)
                {
                    return false;
                }
                return header.Scheme == "Bearer" && header.Parameter == bearerToken;
            }

            static void AssertParametersAreEqual(Dictionary<string, string> expected, string actual)
            {
                NameValueCollection parsedParameters = HttpUtility.ParseQueryString(actual);
                foreach (var parameter in expected)
                {
                    Assert.Equal(parameter.Value, parsedParameters.Get(parameter.Key));
                }
                Assert.Equal(expected.Count, parsedParameters.AllKeys.Length);
            }
        }

        static HttpResponseMessage CreateRequestAuthenticateResponse(string scheme, string parameter)
        {
            var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            response.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(scheme, parameter));
            return response;
        }

        static HttpResponseMessage CreateBearerTokenResponse(string bearerToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            string json =
            $$"""
            {
              "token": "{{bearerToken}}"
            }
            """;
            response.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(json));
            return response;
        }

        private sealed class ServerMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _server;

            public ServerMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> server)
            {
                _server = server;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_server(request));
            }
        }

        /// <summary>
        /// Verifies the scheme allowlist accept path: https realms are always accepted (including
        /// non-default ports and public-routable IP literals), and http realms are accepted only
        /// when the registry is configured as insecure (explicit operator opt-in to downgrade).
        /// </summary>
        [Theory]
        [InlineData("https://auth.example.com/token", false)]
        [InlineData("https://auth.example.com:8443/token", false)]
        [InlineData("https://203.0.113.10/token", false)]          // TEST-NET-3 doc IP, outside every blocked range, must be allowed
        [InlineData("http://auth.example.com/token", true)]        // downgrade permitted only when insecure
        public void ValidateRealmUri_AcceptsAllowedSchemes(string realm, bool isInsecureRegistry)
        {
            Uri uri = AuthHandshakeMessageHandler.ValidateRealmUri(realm, "registry.example.com", isInsecureRegistry);
            Assert.Equal(realm, uri.AbsoluteUri);
        }

        /// <summary>
        /// Verifies that every scheme outside the allowlist is rejected: http on a secure
        /// registry, and any non-http(s) scheme regardless of the insecure flag. Defends
        /// against credential downgrade and exfiltration to non-HTTP transports.
        /// </summary>
        [Theory]
        [InlineData("http://auth.example.com/token", false)] // http on secure registry
        [InlineData("ftp://auth.example.com/token", false)]  // unsupported scheme on secure registry
        [InlineData("ftp://auth.example.com/token", true)]   // unsupported scheme stays rejected even when insecure
        [InlineData("file:///etc/passwd", false)]            // file scheme
        public void ValidateRealmUri_RejectsDisallowedSchemes(string realm, bool isInsecureRegistry)
        {
            Assert.Throws<InvalidAuthResponseException>(() =>
                AuthHandshakeMessageHandler.ValidateRealmUri(realm, "registry.example.com", isInsecureRegistry));
        }

        /// <summary>
        /// Verifies that realm values which fail to parse as an absolute URI (relative paths,
        /// bare hostnames without a scheme, free-form text) are rejected with
        /// <see cref="InvalidAuthResponseException"/> rather than producing an opaque
        /// downstream failure.
        /// </summary>
        [Theory]
        [InlineData("not a url")]
        [InlineData("/relative/path")]
        [InlineData("auth.example.com/token")]
        public void ValidateRealmUri_RejectsRelativeOrUnparseableRealms(string realm)
        {
            Assert.Throws<InvalidAuthResponseException>(() =>
                AuthHandshakeMessageHandler.ValidateRealmUri(realm, "registry.example.com", isInsecureRegistry: false));
        }

        /// <summary>
        /// Verifies the IP-literal guard against the full set of reserved address ranges
        /// (loopback, RFC 1918 private, link-local, link-local multicast, IPv6 unique- and
        /// site-local, unspecified, and IPv4-mapped IPv6). Also verifies the canonicalization
        /// hardening: Unicode-dot forms (U+FF0E, U+3002) that a runtime would resolve back to
        /// a blocked IPv4 literal are rejected even though they appear as DNS-typed hosts.
        /// </summary>
        [Theory]
        // IPv4 ranges that must be blocked.
        [InlineData("https://127.0.0.1/token")]                // loopback
        [InlineData("https://127.5.6.7/token")]                // 127/8
        [InlineData("https://0.0.0.0/token")]                  // unspecified
        [InlineData("https://10.0.0.5/token")]                 // private
        [InlineData("https://172.16.0.1/token")]               // private
        [InlineData("https://172.31.255.255/token")]           // private edge
        [InlineData("https://192.168.1.5/token")]              // private
        [InlineData("https://169.254.169.254/token")]          // link-local (cloud metadata)
        [InlineData("https://224.0.0.1/token")]                // link-local multicast
        // IPv6 ranges that must be blocked.
        [InlineData("https://[::1]/token")]                    // loopback
        [InlineData("https://[::]/token")]                     // unspecified
        [InlineData("https://[fe80::1]/token")]                // link-local
        [InlineData("https://[ff02::1]/token")]                // link-local multicast
        [InlineData("https://[fc00::1]/token")]                // unique-local (private)
        [InlineData("https://[fec0::1]/token")]                // site-local (deprecated, still treated as private)
        [InlineData("https://[::ffff:127.0.0.1]/token")]       // IPv4-mapped IPv6 of loopback
        [InlineData("https://[::ffff:169.254.169.254]/token")] // IPv4-mapped IPv6 of metadata
        // Unicode-dot canonicalization bypasses: U+FF0E (fullwidth full stop) and U+3002
        // (ideographic full stop) appear as DNS to Uri.HostNameType but Uri.IdnHost canonicalizes
        // them back to the underlying IPv4 literal that HttpClient actually connects to.
        [InlineData("https://127\uFF0E0\uFF0E0\uFF0E1/token")]
        [InlineData("https://169\uFF0E254\uFF0E169\uFF0E254/token")]
        [InlineData("https://10\uFF0E0\uFF0E0\uFF0E1/token")]
        [InlineData("https://127\u30020\u30020\u30021/token")]
        // FQDN root-zone trailing dot: Uri.IdnHost preserves the trailing "." so neither
        // IPAddress.TryParse nor a plain DNS name match would catch these without normalization,
        // but every resolver treats "127.0.0.1." as equivalent to "127.0.0.1".
        [InlineData("https://127.0.0.1./token")]
        [InlineData("https://169.254.169.254./token")]
        [InlineData("https://10.0.0.5./token")]
        public void ValidateRealmUri_RejectsBlockedIpLiterals_OnSecureRegistry(string realm)
        {
            Assert.Throws<InvalidAuthResponseException>(() =>
                AuthHandshakeMessageHandler.ValidateRealmUri(realm, "registry.example.com", isInsecureRegistry: false));
        }

        /// <summary>
        /// Verifies that the insecure-registry exception is narrowly scoped: blocked
        /// IP-literal realms are still rejected when their host does not match the registry
        /// host, and lookalike hostnames such as <c>localhost.example.com</c> do not trigger
        /// the RFC 6761 localhost-loopback exception.
        /// </summary>
        [Theory]
        // Even for insecure registries, IP-literal realm hosts are blocked unless they match the registry host.
        [InlineData("https://169.254.169.254/token", "192.168.1.5:5000")]
        [InlineData("https://10.0.0.5/token", "192.168.1.5:5000")]
        [InlineData("https://[::1]/token", "192.168.1.5:5000")]
        // The localhost exception only widens loopback (RFC 6761) - non-loopback blocked IPs
        // are still rejected even when the registry name is "localhost".
        [InlineData("https://169.254.169.254/token", "localhost:5000")]
        [InlineData("https://192.168.1.5/token", "localhost:5000")]
        // A name that merely contains "localhost" but isn't localhost or a *.localhost subdomain
        // does not get the exception (e.g. "localhost.example.com" is a public DNS name).
        [InlineData("https://127.0.0.1/token", "localhost.example.com:5000")]
        public void ValidateRealmUri_RejectsBlockedIpLiterals_OnInsecureRegistryWhenHostsDiffer(string realm, string registryName)
        {
            Assert.Throws<InvalidAuthResponseException>(() =>
                AuthHandshakeMessageHandler.ValidateRealmUri(realm, registryName, isInsecureRegistry: true));
        }

        /// <summary>
        /// Verifies the exception that allows an otherwise-blocked IP-literal realm when the
        /// registry is insecure and the realm host refers to the same machine as the registry
        /// host.
        /// </summary>
        [Theory]
        // Exception: when registry is insecure AND realm host equals the registry host (port-independent),
        // an otherwise-blocked IP literal is permitted to support legitimate private/on-prem dev registries.
        [InlineData("http://192.168.1.5/auth", "192.168.1.5")]
        [InlineData("http://192.168.1.5:6000/auth", "192.168.1.5:5000")] // same host, different port
        [InlineData("https://192.168.1.5/auth", "192.168.1.5:5000")]
        [InlineData("http://127.0.0.1:7000/auth", "127.0.0.1:5000")]
        [InlineData("https://[::1]:7000/auth", "[::1]:5000")]
        // RFC 6761: "localhost" (and *.localhost subdomains) are reserved for loopback, so a
        // localhost-named registry returning a loopback IP-literal realm is legitimate.
        [InlineData("http://127.0.0.1:5000/auth", "localhost:5000")]
        [InlineData("http://127.0.0.1:5000/auth", "LocalHost:5000")]     // case-insensitive
        [InlineData("http://[::1]:5000/auth", "localhost:5000")]
        [InlineData("http://127.0.0.1:5000/auth", "registry.localhost:5000")]
        public void ValidateRealmUri_AllowsMatchingIpLiteralWhenInsecure(string realm, string registryName)
        {
            Uri uri = AuthHandshakeMessageHandler.ValidateRealmUri(realm, registryName, isInsecureRegistry: true);
            Assert.Equal(realm, uri.AbsoluteUri);
        }

        /// <summary>
        /// Verifies that DNS realms whose host is a reserved loopback name (RFC 6761:
        /// <c>localhost</c> or <c>*.localhost</c>) are rejected. These names resolve to
        /// loopback regardless of the host file, so they carry the same risk as a literal
        /// 127.0.0.1 even though they appear as DNS to <c>Uri.HostNameType</c>. Both the
        /// secure-registry case and the insecure-but-non-matching-registry case are covered.
        /// </summary>
        [Theory]
        // Secure registry: loopback-name realms are always rejected.
        [InlineData("https://localhost/token", "registry.example.com", false)]
        [InlineData("https://localhost:5000/token", "registry.example.com", false)]
        [InlineData("https://foo.localhost/token", "registry.example.com", false)]
        [InlineData("https://LOCALHOST/token", "registry.example.com", false)] // case-insensitive
        // FQDN root-zone trailing dot: "localhost." is equivalent to "localhost" to every
        // resolver. Uri.IdnHost preserves the dot so the validator must normalize it away.
        [InlineData("https://localhost./token", "registry.example.com", false)]
        [InlineData("https://foo.localhost./token", "registry.example.com", false)]
        // Unicode trailing dot (U+3002 ideographic full stop) - Uri.IdnHost canonicalizes
        // it to "localhost.", so it must be caught by the same trailing-dot normalization.
        [InlineData("https://localhost\u3002/token", "registry.example.com", false)]
        // Insecure registry: still rejected when registry isn't a loopback-equivalent host.
        [InlineData("https://localhost/token", "192.168.1.5:5000", true)]
        [InlineData("http://localhost/token", "192.168.1.5:5000", true)]
        // Lookalike that isn't actually localhost: "localhost.example.com" is a public DNS
        // name, so the registry doesn't match the loopback exception either.
        [InlineData("http://localhost/token", "localhost.example.com:5000", true)]
        public void ValidateRealmUri_RejectsLoopbackDnsNameRealm(string realm, string registryName, bool isInsecureRegistry)
        {
            Assert.Throws<InvalidAuthResponseException>(() =>
                AuthHandshakeMessageHandler.ValidateRealmUri(realm, registryName, isInsecureRegistry));
        }

        /// <summary>
        /// Verifies that a realm whose host is a reserved loopback DNS name is permitted
        /// when the registry is insecure and the registry host is itself loopback-equivalent
        /// (a loopback IP literal, <c>localhost</c>, or a <c>*.localhost</c> subdomain).
        /// Mirrors <see cref="ValidateRealmUri_AllowsMatchingIpLiteralWhenInsecure"/> for the
        /// case where the realm side uses a DNS name instead of an IP literal.
        /// </summary>
        [Theory]
        [InlineData("http://localhost:5000/auth", "localhost:5000")]
        [InlineData("https://localhost:5000/auth", "localhost:5000")]
        [InlineData("http://localhost:7000/auth", "localhost:5000")]           // port-independent
        [InlineData("http://foo.localhost:5000/auth", "localhost:5000")]       // *.localhost realm
        [InlineData("http://localhost:5000/auth", "registry.localhost:5000")]  // *.localhost registry
        [InlineData("http://localhost:5000/auth", "127.0.0.1:5000")]           // registry is loopback IP literal
        [InlineData("http://localhost:5000/auth", "[::1]:5000")]               // registry is IPv6 loopback literal
        public void ValidateRealmUri_AllowsLoopbackDnsNameRealm_WhenInsecureAndRegistryIsLoopback(string realm, string registryName)
        {
            Uri uri = AuthHandshakeMessageHandler.ValidateRealmUri(realm, registryName, isInsecureRegistry: true);
            Assert.Equal(realm, uri.AbsoluteUri);
        }

        /// <summary>
        /// Verifies that public DNS-named realms (i.e. not RFC 6761 loopback names and not
        /// IP literals) pass all guards regardless of the insecure flag - this is the
        /// expected shape for any production token endpoint.
        /// </summary>
        [Theory]
        [InlineData("https://auth.example.com/token", "registry.example.com", false)]
        [InlineData("https://auth.docker.io/token", "registry-1.docker.io", false)]    // real Docker Hub realm shape
        [InlineData("http://auth.example.com:8080/token", "registry.example.com:5000", true)]
        public void ValidateRealmUri_AllowsPublicDnsRealms(string realm, string registryName, bool isInsecureRegistry)
        {
            Uri uri = AuthHandshakeMessageHandler.ValidateRealmUri(realm, registryName, isInsecureRegistry);
            Assert.Equal(realm, uri.AbsoluteUri);
        }

        /// <summary>
        /// End-to-end verification that an invalid bearer realm in a 401 challenge causes
        /// <see cref="AuthHandshakeMessageHandler.SendAsync"/> to throw
        /// <see cref="InvalidAuthResponseException"/> and dispatch zero requests to the
        /// realm host.
        /// </summary>
        [Fact]
        public async Task SendAsync_ThrowsOnInvalidBearerRealm_WithoutTokenRequest()
        {
            // Use a unique registry name to avoid contamination from the static auth header cache.
            string registryName = $"realm-validation-test-{Guid.NewGuid():N}.invalid";
            string requestUrl = $"https://{registryName}/v2";

            int tokenRequestCount = 0;
            HttpResponseMessage Server(HttpRequestMessage request)
            {
                if (request.RequestUri?.Host != registryName)
                {
                    // Any request to a host other than the registry would indicate the handler attempted a token fetch.
                    // SendAsync is invoked sequentially by the auth retry loop, so no synchronization is needed.
                    tokenRequestCount++;
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }

                // Return a 401 with a Bearer challenge whose realm points at the cloud metadata service.
                var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
                response.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
                    "Bearer",
                    "realm=\"https://169.254.169.254/token\", service=\"evil\""));
                return response;
            }

            var authHandler = new AuthHandshakeMessageHandler(
                registryName,
                isInsecureRegistry: false,
                new ServerMessageHandler(Server),
                NullLogger.Instance,
                RegistryMode.Pull);
            using var httpClient = new HttpClient(authHandler);

            await Assert.ThrowsAsync<InvalidAuthResponseException>(() =>
                httpClient.GetAsync(requestUrl, TestContext.Current.CancellationToken));

            // The handler must not have followed the malicious realm.
            Assert.Equal(0, tokenRequestCount);
        }
    }
}
