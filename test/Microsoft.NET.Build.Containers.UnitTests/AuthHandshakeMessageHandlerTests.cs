// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Sockets;
using System.Web;
using System.Net.Http.Headers;
using System.Collections.Specialized;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.NET.Build.Containers.Tasks;
using Moq;

namespace Microsoft.NET.Build.Containers.UnitTests
{
    [TestClass]
    // Mutates process-global environment variables (registry credentials, REGISTRY_AUTH_FILE) and
    // shares AuthHandshakeMessageHandler's process-wide static credential cache, which is keyed by
    // registry name - the same TestRegistryName for every test here. [DoNotParallelize] keeps
    // these tests from running concurrently, making today's shared-cache behavior deterministic
    // enough for the suite to pass. A [ResourceLock] (even with a custom key for the cache)
    // would serialize these tests but cannot reset that private cache between tests, so later
    // tests can reuse a cached Authorization header and skip the handshake. The real fix is to
    // give each data row its own registry name; see
    // https://github.com/dotnet/sdk/issues/55526.
    [DoNotParallelize]
    public class AuthHandshakeMessageHandlerTests
    {
        public TestContext TestContext { get; set; } = default!;

        private const string TestRegistryName = "registry.test";
        private const string RequestUrl = $"https://{TestRegistryName}/v2";
        private const string BearerRealmUrl = $"https://bearer.test/token";

        [TestMethod]
        [DataRow("SDK_CONTAINER_REGISTRY_UNAME", "SDK_CONTAINER_REGISTRY_PWORD", (int)RegistryMode.Push)]
        [DataRow("DOTNET_CONTAINER_PUSH_REGISTRY_UNAME", "DOTNET_CONTAINER_PUSH_REGISTRY_PWORD", (int)RegistryMode.Push)]
        [DataRow("DOTNET_CONTAINER_PULL_REGISTRY_UNAME", "DOTNET_CONTAINER_PULL_REGISTRY_PWORD", (int)RegistryMode.Pull)]
        [DataRow("DOTNET_CONTAINER_PULL_REGISTRY_UNAME", "DOTNET_CONTAINER_PULL_REGISTRY_PWORD", (int)RegistryMode.PullFromOutput)]
        [DataRow("SDK_CONTAINER_REGISTRY_UNAME", "SDK_CONTAINER_REGISTRY_PWORD", (int)RegistryMode.PullFromOutput)]
        public void GetDockerCredentialsFromEnvironment_ReturnsCorrectValues(string unameVarName, string pwordVarName, int mode)
        {
            string? originalUnameValue = Environment.GetEnvironmentVariable(unameVarName);
            string? originalPwordValue = Environment.GetEnvironmentVariable(pwordVarName);

            Environment.SetEnvironmentVariable(unameVarName, "uname");
            Environment.SetEnvironmentVariable(pwordVarName, "pword");

            try
            {
                if (AuthHandshakeMessageHandler.GetDockerCredentialsFromEnvironment((RegistryMode)mode) is (string credU, string credP))
                {
                    Assert.AreEqual("uname", credU);
                    Assert.AreEqual("pword", credP);
                }
                else
                {
                    Assert.Fail("Should have parsed credentials from environment");
                }
            }
            finally
            {
                // restore env variable values even if an assertion throws
                Environment.SetEnvironmentVariable(unameVarName, originalUnameValue);
                Environment.SetEnvironmentVariable(pwordVarName, originalPwordValue);
            }
        }

        [TestMethod]
        [DynamicData(nameof(GetAuthenticateTestData))]
        public async Task Authenticate(string authConf, Func<HttpRequestMessage, HttpResponseMessage> server)
        {
            string authFile = Path.GetTempFileName();
            string? originalAuthFileValue = Environment.GetEnvironmentVariable("REGISTRY_AUTH_FILE");
            try
            {
                File.WriteAllText(authFile, authConf);
                Environment.SetEnvironmentVariable("REGISTRY_AUTH_FILE", authFile);

                var authHandler = new AuthHandshakeMessageHandler(TestRegistryName, new Uri(RequestUrl), isInsecureRegistry: false, new ServerMessageHandler(server), NullLogger.Instance, RegistryMode.Push);
                using var httpClient = new HttpClient(authHandler);

                var response = await httpClient.GetAsync(RequestUrl, TestContext.CancellationToken);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            }
            finally
            {
                // restore REGISTRY_AUTH_FILE so later tests don't see a stale path
                Environment.SetEnvironmentVariable("REGISTRY_AUTH_FILE", originalAuthFileValue);
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
                        "identitytoken": "{{identityToken}}",
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
                    Assert.AreEqual(method, request.Method);

                    // Verify the query parameter are the expected ones.
                    AssertParametersAreEqual(queryParameters, request.RequestUri.Query);

                    // Verify the auth header is the expected one.
                    AuthenticationHeaderValue? header = request.Headers.Authorization;
                    if (authHeader is not null)
                    {
                        Assert.IsNotNull(header);
                        Assert.AreEqual(header.Scheme, authHeader.Scheme);
                        Assert.AreEqual(header.Parameter, authHeader.Parameter);
                    }
                    else
                    {
                        Assert.IsNull(header);
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
                    Assert.AreEqual(parameter.Value, parsedParameters.Get(parameter.Key));
                }
                Assert.HasCount(expected.Count, parsedParameters.AllKeys);
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
                HttpResponseMessage response = _server(request);
                response.RequestMessage ??= request;
                return Task.FromResult(response);
            }
        }

        private sealed class TrackingContent : HttpContent
        {
            public bool IsDisposed { get; private set; }

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => Task.CompletedTask;

            protected override bool TryComputeLength(out long length)
            {
                length = 0;
                return true;
            }

            protected override void Dispose(bool disposing)
            {
                IsDisposed = disposing;
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Verifies that Basic and Bearer challenges from a redirected request with a different
        /// host, port, or DNS authority are rejected without sending registry credentials.
        /// </summary>
        [TestMethod]
        [DataRow("Basic", "", "different-host", false)]
        [DataRow("Basic", "", "different-port", false)]
        [DataRow("Basic", "", "trailing-dot-host", false)]
        [DataRow("Bearer", "realm=\"https://external.invalid/token\"", "different-host", false)]
        [DataRow("Bearer", "realm=\"https://external.invalid/token\"", "different-port", false)]
        [DataRow("Basic", "", "different-host", true)]
        [DataRow("Basic", "", "different-port", true)]
        [DataRow("Basic", "", "trailing-dot-host", true)]
        [DataRow("Bearer", "realm=\"https://external.invalid/token\"", "different-host", true)]
        [DataRow("Bearer", "realm=\"https://external.invalid/token\"", "different-port", true)]
        [DataRow("Bearer", "realm=\"first\",realm=\"second\"", "different-host", false)]
        [DataRow("Bearer", "realm=\"first\",realm=\"second\"", "different-host", true)]
        [DataRow("Bearer", "realm=\"https://external.invalid/token\",scope=\"first\",scope=\"second\"", "different-host", false)]
        [DataRow("Bearer", "realm=\"https://external.invalid/token\",scope=\"first\",scope=\"second\"", "different-host", true)]
        [DataRow("Bearer", "service=\"registry\"", "different-host", false)]
        [DataRow("Bearer", "service=\"registry\"", "different-host", true)]
        [DataRow("Bearer", "", "different-host", false)]
        [DataRow("Bearer", "", "different-host", true)]
        [DataRow("bEaReR", "realm=\"first\",realm=\"second\"", "different-host", true)]
        [DataRow("bAsIc", "realm=\"first\",realm=\"second\"", "different-host", true)]
        public async Task SendAsync_RejectsAuthenticationChallengeFromRedirectedOrigin(
            string authenticationScheme,
            string authenticationParameters,
            string redirectedOrigin,
            bool authenticateBeforeRedirect)
        {
            // Model a registry request whose final response URI has a different HTTP origin.
            string registryName = $"auth-redirect-test-{Guid.NewGuid():N}.invalid";
            Uri registryUri = new($"https://{registryName}");
            Uri redirectedUri = redirectedOrigin switch
            {
                "different-host" => new Uri("https://external.invalid/v2"),
                "different-port" => new UriBuilder(registryUri) { Port = 444 }.Uri,
                "trailing-dot-host" => new UriBuilder(registryUri) { Host = $"{registryName}." }.Uri,
                _ => throw new ArgumentOutOfRangeException(nameof(redirectedOrigin))
            };
            int requestCount = 0;
            TrackingContent? responseContent = null;

            HttpResponseMessage Server(HttpRequestMessage request)
            {
                requestCount++;
                Assert.AreEqual(registryUri, request.RequestUri);
                if (authenticateBeforeRedirect && requestCount == 1)
                {
                    Assert.IsNull(request.Headers.Authorization);
                    return CreateRequestAuthenticateResponse("Basic", "");
                }

                Assert.AreEqual(authenticateBeforeRedirect ? "Basic" : null, request.Headers.Authorization?.Scheme);
                // Model the transport removing authorization when it follows the redirect.
                request.Headers.Authorization = null;
                request.RequestUri = redirectedUri;
                HttpResponseMessage response = CreateRequestAuthenticateResponse(authenticationScheme, authenticationParameters);
                response.Content = responseContent = new TrackingContent();
                return response;
            }

            await WithRegistryCredentialsAsync(registryName, async () =>
            {
                var authHandler = new AuthHandshakeMessageHandler(
                    registryName,
                    registryUri,
                    isInsecureRegistry: false,
                    new ServerMessageHandler(Server),
                    NullLogger.Instance,
                    RegistryMode.Push);
                using var httpClient = new HttpClient(authHandler);

                // Process the challenge with registry credentials available for lookup.
                await Assert.ThrowsExactlyAsync<InvalidAuthResponseException>(
                    () => httpClient.GetAsync(registryUri, TestContext.CancellationToken));
            });

            // There is no authentication retry after the different-origin challenge,
            // and its response is disposed as part of the rejection.
            Assert.AreEqual(authenticateBeforeRedirect ? 2 : 1, requestCount);
            Assert.IsTrue(responseContent?.IsDisposed);
        }

        /// <summary>
        /// Verifies with the default HTTP transport that a Basic challenge received after an
        /// automatic cross-origin redirect is rejected without sending registry credentials.
        /// </summary>
        [TestMethod]
        [DataRow(301, false)]
        [DataRow(302, false)]
        [DataRow(303, false)]
        [DataRow(307, false)]
        [DataRow(308, false)]
        [DataRow(301, true)]
        [DataRow(302, true)]
        [DataRow(303, true)]
        [DataRow(307, true)]
        [DataRow(308, true)]
        public async Task SendAsync_RejectsAuthenticationChallengeAfterAutomaticRedirect(int redirectStatus, bool authenticateBeforeRedirect)
        {
            // Use separate loopback ports to represent the configured registry and a
            // different origin reached through an automatic redirect.
            using TcpListener registryListener = new(IPAddress.Loopback, 0);
            using TcpListener redirectedListener = new(IPAddress.Loopback, 0);
            registryListener.Start();
            redirectedListener.Start();

            int registryPort = ((IPEndPoint)registryListener.LocalEndpoint).Port;
            int redirectedPort = ((IPEndPoint)redirectedListener.LocalEndpoint).Port;
            string registryName = $"127.0.0.1:{registryPort}";
            Uri registryUri = new($"http://{registryName}");
            Uri redirectedUri = new($"http://127.0.0.1:{redirectedPort}/v2");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));

            Task<string> registryRequest = ServeRegistryAsync();

            // The redirected origin responds with a Basic challenge after receiving the
            // automatically redirected request.
            Task<string> redirectedRequest = SendResponseAsync(
                redirectedListener,
                "HTTP/1.1 401 Unauthorized\r\n"
                    + "WWW-Authenticate: Basic realm=\"registry\"\r\n"
                    + "Content-Length: 0\r\n"
                    + "Connection: close\r\n\r\n",
                timeout.Token,
                stopListenerAfterResponse: true);

            // Exercise the real SocketsHttpHandler redirect path with credentials configured
            // for the original registry.
            InvalidAuthResponseException exception = await WithRegistryCredentialsAsync(registryName, async () =>
            {
                var authHandler = new AuthHandshakeMessageHandler(
                    registryName,
                    registryUri,
                    isInsecureRegistry: true,
                    new SocketsHttpHandler { UseCookies = false, UseProxy = false },
                    NullLogger.Instance,
                    RegistryMode.Push);
                using var httpClient = new HttpClient(authHandler);

                return await Assert.ThrowsExactlyAsync<InvalidAuthResponseException>(
                    () => httpClient.GetAsync(new Uri(registryUri, "/v2"), timeout.Token));
            });

            string initialRequestHeaders = await registryRequest;
            string redirectedRequestHeaders = await redirectedRequest;

            Assert.AreEqual(authenticateBeforeRedirect, initialRequestHeaders.Contains("Authorization: Basic ", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(redirectedRequestHeaders.Contains("Authorization:", StringComparison.OrdinalIgnoreCase));

            // The failure identifies both the observed challenge origin and the configured
            // registry origin to make the rejected workflow diagnosable.
            Assert.Contains("CONTAINER1019", exception.Message);
            Assert.Contains(redirectedUri.GetLeftPart(UriPartial.Authority), exception.Message);
            Assert.Contains(registryUri.GetLeftPart(UriPartial.Authority), exception.Message);

            async Task<string> ServeRegistryAsync()
            {
                if (authenticateBeforeRedirect)
                {
                    string unauthenticatedHeaders = await SendResponseAsync(
                        registryListener,
                        "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Basic realm=\"registry\"\r\n"
                            + "Content-Length: 0\r\nConnection: close\r\n\r\n",
                        timeout.Token);
                    Assert.IsFalse(unauthenticatedHeaders.Contains("Authorization:", StringComparison.OrdinalIgnoreCase));
                }

                return await SendResponseAsync(
                    registryListener,
                    $"HTTP/1.1 {redirectStatus} Redirect\r\nLocation: {redirectedUri.AbsoluteUri}\r\n"
                        + "Content-Length: 0\r\nConnection: close\r\n\r\n",
                    timeout.Token,
                    stopListenerAfterResponse: true);
            }
        }

        /// <summary>
        /// The authenticated retry is not another authentication cycle. Same-origin challenges
        /// and responses without a supported 401 challenge are returned unchanged.
        /// </summary>
        [TestMethod]
        [DataRow(HttpStatusCode.Unauthorized, "Basic", true)]
        [DataRow(HttpStatusCode.Unauthorized, "Bearer realm=\"https://auth.invalid/token\"", true)]
        [DataRow(HttpStatusCode.Unauthorized, "Bearer realm=\"first\",realm=\"second\"", true)]
        [DataRow(HttpStatusCode.OK, null, false)]
        [DataRow(HttpStatusCode.Unauthorized, null, false)]
        [DataRow(HttpStatusCode.Unauthorized, "Digest", false)]
        [DataRow(HttpStatusCode.Forbidden, "Basic", false)]
        public async Task SendAsync_ReturnsAuthenticatedRetryResponseWithoutFurtherAuthentication(
            HttpStatusCode statusCode, string? challenge, bool sameOrigin)
        {
            string registryName = $"auth-retry-test-{Guid.NewGuid():N}.invalid";
            Uri registryUri = new($"https://{registryName}");
            int requestCount = 0;
            var responseContent = new TrackingContent();
            using var retryResponse = new HttpResponseMessage(statusCode) { Content = responseContent };
            if (challenge is not null)
            {
                retryResponse.Headers.WwwAuthenticate.Add(AuthenticationHeaderValue.Parse(challenge));
            }

            HttpResponseMessage Server(HttpRequestMessage request)
            {
                requestCount++;
                if (requestCount == 1)
                {
                    Assert.IsNull(request.Headers.Authorization);
                    return CreateRequestAuthenticateResponse("Basic", "");
                }

                Assert.AreEqual(2, requestCount);
                Assert.AreEqual("Basic", request.Headers.Authorization?.Scheme);
                request.RequestUri = sameOrigin ? new Uri(registryUri, "/redirected") : new Uri("https://storage.invalid/blob");
                return retryResponse;
            }

            await WithRegistryCredentialsAsync(registryName, async () =>
            {
                var authHandler = new AuthHandshakeMessageHandler(
                    registryName, registryUri, isInsecureRegistry: false,
                    new ServerMessageHandler(Server), NullLogger.Instance, RegistryMode.Push);
                using var httpClient = new HttpClient(authHandler);
                using HttpResponseMessage response = await httpClient.GetAsync(registryUri, TestContext.CancellationToken);

                Assert.AreSame(retryResponse, response);
                Assert.IsFalse(responseContent.IsDisposed);
                Assert.AreEqual(2, requestCount);
            });
        }

        /// <summary>
        /// Verifies that an HTTP authentication challenge is rejected for a registry configured
        /// to use HTTPS, even when the host and effective port match.
        /// </summary>
        [TestMethod]
        public async Task SendAsync_RejectsHttpAuthenticationChallengeForSecureRegistry()
        {
            // Model a secure registry whose request ends at HTTP on the same host and port.
            string registryName = $"secure-scheme-test-{Guid.NewGuid():N}.invalid";
            Uri registryUri = new($"https://{registryName}");
            Uri redirectedUri = new($"http://{registryName}:443/v2");
            int requestCount = 0;

            HttpResponseMessage Server(HttpRequestMessage request)
            {
                // Return the Basic challenge from the downgraded final URI.
                requestCount++;
                request.RequestUri = redirectedUri;
                return CreateRequestAuthenticateResponse("Basic", "");
            }

            // Process the challenge with credentials configured for the HTTPS registry.
            InvalidAuthResponseException exception = await WithRegistryCredentialsAsync(registryName, async () =>
            {
                var authHandler = new AuthHandshakeMessageHandler(
                    registryName,
                    registryUri,
                    isInsecureRegistry: false,
                    new ServerMessageHandler(Server),
                    NullLogger.Instance,
                    RegistryMode.Push);
                using var httpClient = new HttpClient(authHandler);

                return await Assert.ThrowsExactlyAsync<InvalidAuthResponseException>(
                    () => httpClient.GetAsync(registryUri, TestContext.CancellationToken));
            });

            // A secure registry does not permit the insecure fallback exception, so the
            // challenge is rejected without an authenticated retry.
            Assert.AreEqual(1, requestCount);
            Assert.Contains("CONTAINER1019", exception.Message);
            Assert.Contains(redirectedUri.GetLeftPart(UriPartial.Authority), exception.Message);
            Assert.Contains(registryUri.GetLeftPart(UriPartial.Authority), exception.Message);
        }

        /// <summary>
        /// Verifies that a Basic challenge after a redirect within the configured registry origin
        /// is accepted and the redirected request is retried with authorization.
        /// </summary>
        [TestMethod]
        public async Task SendAsync_AllowsBasicChallengeFromSameOriginRedirect()
        {
            // Model a redirect that changes only the path within the configured registry origin.
            string registryName = $"basic-same-origin-test-{Guid.NewGuid():N}.invalid";
            Uri registryUri = new($"https://{registryName}");
            Uri redirectedUri = new(registryUri, "/redirected");
            int requestCount = 0;

            HttpResponseMessage Server(HttpRequestMessage request)
            {
                requestCount++;
                if (requestCount == 1)
                {
                    // The first response represents a Basic challenge received after the
                    // same-origin redirect.
                    request.RequestUri = redirectedUri;
                    return CreateRequestAuthenticateResponse("Basic", "");
                }

                // The accepted challenge causes a retry of the redirected URI with Basic auth.
                Assert.AreEqual(redirectedUri, request.RequestUri);
                Assert.AreEqual("Basic", request.Headers.Authorization?.Scheme);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            // Process the same-origin challenge with matching registry credentials available.
            await WithRegistryCredentialsAsync(registryName, async () =>
            {
                var authHandler = new AuthHandshakeMessageHandler(
                    registryName,
                    registryUri,
                    isInsecureRegistry: false,
                    new ServerMessageHandler(Server),
                    NullLogger.Instance,
                    RegistryMode.Push);
                using var httpClient = new HttpClient(authHandler);

                using HttpResponseMessage response = await httpClient.GetAsync(registryUri, TestContext.CancellationToken);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            });

            // The workflow consists of the unauthenticated challenge followed by one
            // authenticated retry.
            Assert.AreEqual(2, requestCount);
        }

        /// <summary>
        /// Verifies that an authorization header cached for the configured registry is not added
        /// to a subsequent request targeting a different origin.
        /// </summary>
        [TestMethod]
        public async Task SendAsync_DoesNotSendCachedCredentialsToDifferentOrigin()
        {
            // Use one handler for the configured registry and a later absolute request to
            // a different origin, reproducing the point where cached auth could be reused.
            string registryName = $"basic-cache-test-{Guid.NewGuid():N}.invalid";
            Uri registryUri = new($"https://{registryName}");
            Uri externalUri = new("https://storage.invalid/blob");
            int registryRequestCount = 0;
            AuthenticationHeaderValue? externalAuthorization = null;

            HttpResponseMessage Server(HttpRequestMessage request)
            {
                if (request.RequestUri == externalUri)
                {
                    // Capture any authorization attached before the different-origin request
                    // reaches the inner transport.
                    externalAuthorization = request.Headers.Authorization;
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }

                // Challenge the first registry request, then accept its authenticated retry;
                // this populates the handler's registry authorization cache.
                registryRequestCount++;
                return request.Headers.Authorization?.Scheme == "Basic"
                    ? new HttpResponseMessage(HttpStatusCode.OK)
                    : CreateRequestAuthenticateResponse("Basic", "");
            }

            await WithRegistryCredentialsAsync(registryName, async () =>
            {
                var authHandler = new AuthHandshakeMessageHandler(
                    registryName,
                    registryUri,
                    isInsecureRegistry: false,
                    new ServerMessageHandler(Server),
                    NullLogger.Instance,
                    RegistryMode.Push);
                using var httpClient = new HttpClient(authHandler);

                // Authenticate to the configured registry to establish cached authorization.
                using HttpResponseMessage registryResponse = await httpClient.GetAsync(registryUri, TestContext.CancellationToken);
                Assert.AreEqual(HttpStatusCode.OK, registryResponse.StatusCode);

                // Send a subsequent request through the same handler to another origin.
                using HttpResponseMessage externalResponse = await httpClient.GetAsync(externalUri, TestContext.CancellationToken);
                Assert.AreEqual(HttpStatusCode.OK, externalResponse.StatusCode);
            });

            // Registry authentication completes normally, but its cached header is not
            // applied to the different-origin request.
            Assert.AreEqual(2, registryRequestCount);
            Assert.IsNull(externalAuthorization);
        }

        /// <summary>
        /// Verifies that the composed authentication and fallback handlers authenticate an explicitly
        /// insecure registry on port 80 when no port was configured, or on its explicitly configured port.
        /// Subsequent HTTPS and HTTP requests reuse cached Basic authorization at the HTTP fallback origin.
        /// </summary>
        [TestMethod]
        [DataRow("", 80)]
        [DataRow(":443", 443)]
        [DataRow(":5000", 5000)]
        public async Task SendAsync_AllowsBasicChallengeAfterInsecureRegistryFallback(string registryPortSuffix, int expectedHttpPort)
        {
            // Isolate the static credential cache and distinguish an implicit HTTPS port
            // from an explicitly configured port, including HTTPS's default port 443.
            string registryHost = $"basic-insecure-test-{Guid.NewGuid():N}.invalid";
            string registryName = $"{registryHost}{registryPortSuffix}";
            Uri registryUri = new($"https://{registryName}");
            Uri requestUri = new(registryUri, "/v2");
            Uri fallbackUri = new($"http://{registryHost}:{expectedHttpPort}/v2");
            int httpsRequestCount = 0;
            int httpRequestCount = 0;

            HttpResponseMessage Server(HttpRequestMessage request)
            {
                if (request.RequestUri!.Scheme == Uri.UriSchemeHttps)
                {
                    // Simulate TLS failure; the real fallback handler must choose the HTTP URI.
                    httpsRequestCount++;
                    Assert.IsNull(request.Headers.Authorization);
                    throw new HttpRequestException(HttpRequestError.SecureConnectionError);
                }

                Assert.AreEqual(fallbackUri, request.RequestUri);
                httpRequestCount++;
                if (httpRequestCount == 1)
                {
                    // The first HTTP request challenges authentication at the fallback origin.
                    Assert.IsNull(request.Headers.Authorization);
                    return CreateRequestAuthenticateResponse("Basic", "");
                }

                // Accept the authenticated retry and later requests using the cached header.
                Assert.AreEqual("Basic", request.Headers.Authorization?.Scheme);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            await WithRegistryCredentialsAsync(registryName, async () =>
            {
                // Use the production handler order; only the underlying transport is simulated.
                var fallbackHandler = new FallbackToHttpMessageHandler(
                    registryName, registryUri.Host, registryUri.Port, new ServerMessageHandler(Server), NullLogger.Instance);
                var authHandler = new AuthHandshakeMessageHandler(
                    registryName,
                    registryUri,
                    isInsecureRegistry: true,
                    fallbackHandler,
                    NullLogger.Instance,
                    RegistryMode.Push);
                using var httpClient = new HttpClient(authHandler);

                // Fail TLS, fall back to HTTP, then authenticate the registry's Basic challenge.
                using HttpResponseMessage response = await httpClient.GetAsync(requestUri, TestContext.CancellationToken);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

                // A later HTTPS request uses both cached authorization and the remembered fallback.
                using HttpResponseMessage cachedResponse = await httpClient.GetAsync(requestUri, TestContext.CancellationToken);
                Assert.AreEqual(HttpStatusCode.OK, cachedResponse.StatusCode);

                // A direct request to the permitted HTTP origin also receives cached authorization.
                using HttpResponseMessage httpResponse = await httpClient.GetAsync(fallbackUri, TestContext.CancellationToken);
                Assert.AreEqual(HttpStatusCode.OK, httpResponse.StatusCode);
            });

            // Only the initial request attempts TLS; HTTP gets one challenge and three authorized requests.
            Assert.AreEqual(1, httpsRequestCount);
            Assert.AreEqual(4, httpRequestCount);
        }

        /// <summary>
        /// Verifies that an insecure registry's HTTP fallback does not allow authentication at a
        /// different host or port reached by a redirect, for implicit and explicitly configured ports.
        /// </summary>
        [TestMethod]
        [DataRow("", 80, "different-host")]
        [DataRow("", 80, "different-port")]
        [DataRow(":443", 443, "different-host")]
        [DataRow(":443", 443, "different-port")]
        [DataRow(":5000", 5000, "different-host")]
        [DataRow(":5000", 5000, "different-port")]
        public async Task SendAsync_RejectsDifferentOriginChallengeAfterInsecureRegistryFallback(
            string registryPortSuffix, int expectedHttpPort, string redirectedOrigin)
        {
            // Keep each credential-cache key unique and change only one part of the HTTP origin.
            string registryHost = $"insecure-redirect-test-{Guid.NewGuid():N}.invalid";
            string registryName = $"{registryHost}{registryPortSuffix}";
            Uri registryUri = new($"https://{registryName}");
            Uri requestUri = new(registryUri, "/v2");
            Uri fallbackUri = new($"http://{registryHost}:{expectedHttpPort}/v2");
            Uri redirectedUri = redirectedOrigin switch
            {
                "different-host" => new UriBuilder(fallbackUri) { Host = "external.invalid" }.Uri,
                "different-port" => new UriBuilder(fallbackUri) { Port = expectedHttpPort + 1 }.Uri,
                _ => throw new ArgumentOutOfRangeException(nameof(redirectedOrigin))
            };
            int requestCount = 0;

            HttpResponseMessage Server(HttpRequestMessage request)
            {
                requestCount++;
                Assert.IsNull(request.Headers.Authorization);
                if (request.RequestUri!.Scheme == Uri.UriSchemeHttps)
                {
                    // Trigger the real fallback handler before simulating any redirect.
                    throw new HttpRequestException(HttpRequestError.SecureConnectionError);
                }

                // The HTTP fallback reaches a different origin, which returns a Basic challenge.
                Assert.AreEqual(fallbackUri, request.RequestUri);
                request.RequestUri = redirectedUri;
                return CreateRequestAuthenticateResponse("Basic", "");
            }

            InvalidAuthResponseException exception = await WithRegistryCredentialsAsync(registryName, async () =>
            {
                var fallbackHandler = new FallbackToHttpMessageHandler(
                    registryName, registryUri.Host, registryUri.Port, new ServerMessageHandler(Server), NullLogger.Instance);
                var authHandler = new AuthHandshakeMessageHandler(
                    registryName, registryUri, isInsecureRegistry: true, fallbackHandler, NullLogger.Instance, RegistryMode.Push);
                using var httpClient = new HttpClient(authHandler);

                // Available registry credentials must not make the different-origin challenge acceptable.
                return await Assert.ThrowsExactlyAsync<InvalidAuthResponseException>(
                    () => httpClient.GetAsync(requestUri, TestContext.CancellationToken));
            });

            // Reject the challenge after TLS failure and HTTP fallback, without an authenticated retry.
            Assert.AreEqual(2, requestCount);
            Assert.Contains(redirectedUri.GetLeftPart(UriPartial.Authority), exception.Message);
            Assert.Contains(registryUri.GetLeftPart(UriPartial.Authority), exception.Message);
        }

        /// <summary>
        /// Verifies that a cross-origin Basic or Bearer challenge during base-image manifest retrieval
        /// makes CreateNewImage fail with a structured CONTAINER1019 diagnostic, even when the
        /// challenge parameters are malformed or missing.
        /// </summary>
        [TestMethod]
        [DataRow(false, "Basic realm=\"registry\"")]
        [DataRow(true, "Basic realm=\"registry\"")]
        [DataRow(false, "Bearer realm=\"first\",realm=\"second\"")]
        [DataRow(true, "Bearer realm=\"first\",realm=\"second\"")]
        [DataRow(false, "Bearer")]
        [DataRow(true, "Bearer")]
        public async Task CreateNewImage_ReportsUnexpectedAuthOriginOnPull(bool authenticateBeforeRedirect, string challenge)
        {
            using TcpListener registryListener = new(IPAddress.Loopback, 0);
            using TcpListener redirectedListener = new(IPAddress.Loopback, 0);
            registryListener.Start();
            redirectedListener.Start();
            string registryName = $"127.0.0.1:{((IPEndPoint)registryListener.LocalEndpoint).Port}";
            Uri redirectedUri = new($"http://127.0.0.1:{((IPEndPoint)redirectedListener.LocalEndpoint).Port}/v2/");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            string runtimeGraphPath = Path.GetTempFileName();
            string? originalInsecureRegistries = Environment.GetEnvironmentVariable("DOTNET_CONTAINER_INSECURE_REGISTRIES");
            try
            {
                // The task constructs an HTTPS URI. Configure this local HTTP-only registry
                // as insecure so the production transport can fall back before the redirect.
                Environment.SetEnvironmentVariable("DOTNET_CONTAINER_INSECURE_REGISTRIES", registryName);
                File.WriteAllText(runtimeGraphPath, "{}");
                Task<string> registryRequest = ServeRegistryAsync();
                Task<string> redirectedRequest = SendResponseAsync(
                    redirectedListener,
                    "HTTP/1.1 401 Unauthorized\r\n"
                        + $"WWW-Authenticate: {challenge}\r\n"
                        + "Content-Length: 0\r\nConnection: close\r\n\r\n",
                    timeout.Token,
                    stopListenerAfterResponse: true);

                var errors = new List<BuildErrorEventArgs>();
                var engine = new Mock<IBuildEngine>();
                engine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>())).Callback<BuildErrorEventArgs>(errors.Add);
                using var task = new CreateNewImage
                {
                    BuildEngine = engine.Object,
                    BaseRegistry = registryName,
                    BaseImageName = "base/image",
                    BaseImageTag = "latest",
                    OutputRegistry = "output.invalid",
                    Repository = "test/image",
                    ImageTags = ["latest"],
                    PublishDirectory = Path.GetTempPath(),
                    RuntimeIdentifierGraphPath = runtimeGraphPath,
                    ContainerRuntimeIdentifier = "linux-x64",
                };

                // Manifest retrieval follows the registry's redirect and rejects the
                // other origin's challenge before any image construction or publication.
                bool succeeded = await WithRegistryCredentialsAsync(registryName, () => task.ExecuteAsync(timeout.Token));
                await Task.WhenAll(registryRequest, redirectedRequest);

                Assert.IsFalse(succeeded);
                Assert.AreEqual(authenticateBeforeRedirect, registryRequest.Result.Contains("Authorization: Basic ", StringComparison.OrdinalIgnoreCase));
                Assert.IsFalse(redirectedRequest.Result.Contains("Authorization:", StringComparison.OrdinalIgnoreCase));
                Assert.HasCount(1, errors);
                Assert.AreEqual("CONTAINER1019", errors[0].Code);
                Assert.Contains($"https://{registryName}", errors[0].Message!);
                Assert.Contains(redirectedUri.GetLeftPart(UriPartial.Authority), errors[0].Message!);
            }
            finally
            {
                Environment.SetEnvironmentVariable("DOTNET_CONTAINER_INSECURE_REGISTRIES", originalInsecureRegistries);
                File.Delete(runtimeGraphPath);
            }

            async Task<string> ServeRegistryAsync()
            {
                // Reject the initial TLS handshake as an HTTP-only server would.
                using (TcpClient client = await registryListener.AcceptTcpClientAsync(timeout.Token))
                {
                    using NetworkStream stream = client.GetStream();
                    await stream.ReadExactlyAsync(new byte[5], timeout.Token);
                    await stream.WriteAsync(
                        "HTTP/1.1 400 Bad Request\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8.ToArray(),
                        timeout.Token);
                }

                if (authenticateBeforeRedirect)
                {
                    string unauthenticatedHeaders = await SendResponseAsync(
                        registryListener,
                        "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Basic realm=\"registry\"\r\n"
                            + "Content-Length: 0\r\nConnection: close\r\n\r\n",
                        timeout.Token);
                    Assert.IsFalse(unauthenticatedHeaders.Contains("Authorization:", StringComparison.OrdinalIgnoreCase));
                }

                return await SendResponseAsync(
                    registryListener,
                    $"HTTP/1.1 302 Found\r\nLocation: {redirectedUri.AbsoluteUri}\r\n"
                        + "Content-Length: 0\r\nConnection: close\r\n\r\n",
                    timeout.Token,
                    stopListenerAfterResponse: true);
            }
        }

        private static async Task<string> SendResponseAsync(
            TcpListener listener,
            string response,
            CancellationToken cancellationToken,
            bool stopListenerAfterResponse = false)
        {
            using TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken);
            using NetworkStream stream = client.GetStream();
            string request = await ReadHttpHeadersAsync(stream, cancellationToken);
            await stream.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken);

            if (stopListenerAfterResponse)
            {
                listener.Stop();
            }

            return request;
        }

        private static async Task<string> ReadHttpHeadersAsync(Stream stream, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[1024];
            using MemoryStream request = new();
            while (request.Length < 16 * 1024)
            {
                int bytesRead = await stream.ReadAsync(buffer, cancellationToken);
                if (bytesRead == 0)
                {
                    break;
                }

                request.Write(buffer, 0, bytesRead);
                string requestText = Encoding.ASCII.GetString(request.GetBuffer(), 0, checked((int)request.Length));
                if (requestText.Contains("\r\n\r\n", StringComparison.Ordinal))
                {
                    return requestText;
                }
            }

            throw new InvalidDataException("HTTP request headers were incomplete.");
        }

        private static async Task WithRegistryCredentialsAsync(string registryName, Func<Task> action)
        {
            await WithRegistryCredentialsAsync(
                registryName,
                async () =>
                {
                    await action();
                    return true;
                });
        }

        private static async Task<T> WithRegistryCredentialsAsync<T>(string registryName, Func<Task<T>> action)
        {
            string authFile = Path.GetTempFileName();
            string? originalAuthFileValue = Environment.GetEnvironmentVariable("REGISTRY_AUTH_FILE");
            try
            {
                File.WriteAllText(
                    authFile,
                    $$"""
                    {
                        "auths": {
                            "{{registryName}}": {
                                "auth": "{{GetUserPasswordBase64("user", "pass")}}"
                            }
                        }
                    }
                    """);
                Environment.SetEnvironmentVariable("REGISTRY_AUTH_FILE", authFile);
                return await action();
            }
            finally
            {
                Environment.SetEnvironmentVariable("REGISTRY_AUTH_FILE", originalAuthFileValue);
                File.Delete(authFile);
            }
        }

        /// <summary>
        /// Verifies the scheme allowlist accept path: https realms are always accepted (including
        /// non-default ports and public-routable IP literals), and http realms are accepted only
        /// when the registry is configured as insecure (explicit operator opt-in to downgrade).
        /// </summary>
        [TestMethod]
        [DataRow("https://auth.example.com/token", false)]
        [DataRow("https://auth.example.com:8443/token", false)]
        [DataRow("https://203.0.113.10/token", false)]          // TEST-NET-3 doc IP, outside every blocked range, must be allowed
        [DataRow("http://auth.example.com/token", true)]        // downgrade permitted only when insecure
        public void ValidateRealmUri_AcceptsAllowedSchemes(string realm, bool isInsecureRegistry)
        {
            Uri uri = AuthHandshakeMessageHandler.ValidateRealmUri(realm, "registry.example.com", isInsecureRegistry);
            Assert.AreEqual(realm, uri.AbsoluteUri);
        }

        /// <summary>
        /// Verifies that every scheme outside the allowlist is rejected: http on a secure
        /// registry, and any non-http(s) scheme regardless of the insecure flag. Defends
        /// against credential downgrade and exfiltration to non-HTTP transports.
        /// </summary>
        [TestMethod]
        [DataRow("http://auth.example.com/token", false)] // http on secure registry
        [DataRow("ftp://auth.example.com/token", false)]  // unsupported scheme on secure registry
        [DataRow("ftp://auth.example.com/token", true)]   // unsupported scheme stays rejected even when insecure
        [DataRow("file:///etc/passwd", false)]            // file scheme
        public void ValidateRealmUri_RejectsDisallowedSchemes(string realm, bool isInsecureRegistry)
        {
            Assert.ThrowsExactly<InvalidAuthResponseException>(() =>
                AuthHandshakeMessageHandler.ValidateRealmUri(realm, "registry.example.com", isInsecureRegistry));
        }

        /// <summary>
        /// Verifies that realm values which fail to parse as an absolute URI (relative paths,
        /// bare hostnames without a scheme, free-form text) are rejected with
        /// <see cref="InvalidAuthResponseException"/> rather than producing an opaque
        /// downstream failure.
        /// </summary>
        [TestMethod]
        [DataRow("not a url")]
        [DataRow("/relative/path")]
        [DataRow("auth.example.com/token")]
        public void ValidateRealmUri_RejectsRelativeOrUnparseableRealms(string realm)
        {
            Assert.ThrowsExactly<InvalidAuthResponseException>(() =>
                AuthHandshakeMessageHandler.ValidateRealmUri(realm, "registry.example.com", isInsecureRegistry: false));
        }

        /// <summary>
        /// Verifies the IP-literal guard against the full set of reserved address ranges
        /// (loopback, RFC 1918 private, link-local, link-local multicast, IPv6 unique- and
        /// site-local, unspecified, and IPv4-mapped IPv6). Also verifies the canonicalization
        /// hardening: Unicode-dot forms (U+FF0E, U+3002) that a runtime would resolve back to
        /// a blocked IPv4 literal are rejected even though they appear as DNS-typed hosts.
        /// </summary>
        [TestMethod]
        // IPv4 ranges that must be blocked.
        [DataRow("https://127.0.0.1/token")]                // loopback
        [DataRow("https://127.5.6.7/token")]                // 127/8
        [DataRow("https://0.0.0.0/token")]                  // unspecified
        [DataRow("https://10.0.0.5/token")]                 // private
        [DataRow("https://172.16.0.1/token")]               // private
        [DataRow("https://172.31.255.255/token")]           // private edge
        [DataRow("https://192.168.1.5/token")]              // private
        [DataRow("https://169.254.169.254/token")]          // link-local (cloud metadata)
        [DataRow("https://224.0.0.1/token")]                // link-local multicast
        // IPv6 ranges that must be blocked.
        [DataRow("https://[::1]/token")]                    // loopback
        [DataRow("https://[::]/token")]                     // unspecified
        [DataRow("https://[fe80::1]/token")]                // link-local
        [DataRow("https://[ff02::1]/token")]                // link-local multicast
        [DataRow("https://[fc00::1]/token")]                // unique-local (private)
        [DataRow("https://[fec0::1]/token")]                // site-local (deprecated, still treated as private)
        [DataRow("https://[::ffff:127.0.0.1]/token")]       // IPv4-mapped IPv6 of loopback
        [DataRow("https://[::ffff:169.254.169.254]/token")] // IPv4-mapped IPv6 of metadata
        // Unicode-dot canonicalization bypasses: U+FF0E (fullwidth full stop) and U+3002
        // (ideographic full stop) appear as DNS to Uri.HostNameType but Uri.IdnHost canonicalizes
        // them back to the underlying IPv4 literal that HttpClient actually connects to.
        [DataRow("https://127\uFF0E0\uFF0E0\uFF0E1/token")]
        [DataRow("https://169\uFF0E254\uFF0E169\uFF0E254/token")]
        [DataRow("https://10\uFF0E0\uFF0E0\uFF0E1/token")]
        [DataRow("https://127\u30020\u30020\u30021/token")]
        // FQDN root-zone trailing dot: Uri.IdnHost preserves the trailing "." so neither
        // IPAddress.TryParse nor a plain DNS name match would catch these without normalization,
        // but every resolver treats "127.0.0.1." as equivalent to "127.0.0.1".
        [DataRow("https://127.0.0.1./token")]
        [DataRow("https://169.254.169.254./token")]
        [DataRow("https://10.0.0.5./token")]
        public void ValidateRealmUri_RejectsBlockedIpLiterals_OnSecureRegistry(string realm)
        {
            Assert.ThrowsExactly<InvalidAuthResponseException>(() =>
                AuthHandshakeMessageHandler.ValidateRealmUri(realm, "registry.example.com", isInsecureRegistry: false));
        }

        /// <summary>
        /// Verifies that the insecure-registry exception is narrowly scoped: blocked
        /// IP-literal realms are still rejected when their host does not match the registry
        /// host, and lookalike hostnames such as <c>localhost.example.com</c> do not trigger
        /// the RFC 6761 localhost-loopback exception.
        /// </summary>
        [TestMethod]
        // Even for insecure registries, IP-literal realm hosts are blocked unless they match the registry host.
        [DataRow("https://169.254.169.254/token", "192.168.1.5:5000")]
        [DataRow("https://10.0.0.5/token", "192.168.1.5:5000")]
        [DataRow("https://[::1]/token", "192.168.1.5:5000")]
        // The localhost exception only widens loopback (RFC 6761) - non-loopback blocked IPs
        // are still rejected even when the registry name is "localhost".
        [DataRow("https://169.254.169.254/token", "localhost:5000")]
        [DataRow("https://192.168.1.5/token", "localhost:5000")]
        // A name that merely contains "localhost" but isn't localhost or a *.localhost subdomain
        // does not get the exception (e.g. "localhost.example.com" is a public DNS name).
        [DataRow("https://127.0.0.1/token", "localhost.example.com:5000")]
        public void ValidateRealmUri_RejectsBlockedIpLiterals_OnInsecureRegistryWhenHostsDiffer(string realm, string registryName)
        {
            Assert.ThrowsExactly<InvalidAuthResponseException>(() =>
                AuthHandshakeMessageHandler.ValidateRealmUri(realm, registryName, isInsecureRegistry: true));
        }

        /// <summary>
        /// Verifies the exception that allows an otherwise-blocked IP-literal realm when the
        /// registry is insecure and the realm host refers to the same machine as the registry
        /// host.
        /// </summary>
        [TestMethod]
        // Exception: when registry is insecure AND realm host equals the registry host (port-independent),
        // an otherwise-blocked IP literal is permitted to support legitimate private/on-prem dev registries.
        [DataRow("http://192.168.1.5/auth", "192.168.1.5")]
        [DataRow("http://192.168.1.5:6000/auth", "192.168.1.5:5000")] // same host, different port
        [DataRow("https://192.168.1.5/auth", "192.168.1.5:5000")]
        [DataRow("http://127.0.0.1:7000/auth", "127.0.0.1:5000")]
        [DataRow("https://[::1]:7000/auth", "[::1]:5000")]
        // RFC 6761: "localhost" (and *.localhost subdomains) are reserved for loopback, so a
        // localhost-named registry returning a loopback IP-literal realm is legitimate.
        [DataRow("http://127.0.0.1:5000/auth", "localhost:5000")]
        [DataRow("http://127.0.0.1:5000/auth", "LocalHost:5000")]     // case-insensitive
        [DataRow("http://[::1]:5000/auth", "localhost:5000")]
        [DataRow("http://127.0.0.1:5000/auth", "registry.localhost:5000")]
        public void ValidateRealmUri_AllowsMatchingIpLiteralWhenInsecure(string realm, string registryName)
        {
            Uri uri = AuthHandshakeMessageHandler.ValidateRealmUri(realm, registryName, isInsecureRegistry: true);
            Assert.AreEqual(realm, uri.AbsoluteUri);
        }

        /// <summary>
        /// Verifies that DNS realms whose host is a reserved loopback name (RFC 6761:
        /// <c>localhost</c> or <c>*.localhost</c>) are rejected. These names resolve to
        /// loopback regardless of the host file, so they carry the same risk as a literal
        /// 127.0.0.1 even though they appear as DNS to <c>Uri.HostNameType</c>. Both the
        /// secure-registry case and the insecure-but-non-matching-registry case are covered.
        /// </summary>
        [TestMethod]
        // Secure registry: loopback-name realms are always rejected.
        [DataRow("https://localhost/token", "registry.example.com", false)]
        [DataRow("https://localhost:5000/token", "registry.example.com", false)]
        [DataRow("https://foo.localhost/token", "registry.example.com", false)]
        [DataRow("https://LOCALHOST/token", "registry.example.com", false)] // case-insensitive
        // FQDN root-zone trailing dot: "localhost." is equivalent to "localhost" to every
        // resolver. Uri.IdnHost preserves the dot so the validator must normalize it away.
        [DataRow("https://localhost./token", "registry.example.com", false)]
        [DataRow("https://foo.localhost./token", "registry.example.com", false)]
        // Unicode trailing dot (U+3002 ideographic full stop) - Uri.IdnHost canonicalizes
        // it to "localhost.", so it must be caught by the same trailing-dot normalization.
        [DataRow("https://localhost\u3002/token", "registry.example.com", false)]
        // Insecure registry: still rejected when registry isn't a loopback-equivalent host.
        [DataRow("https://localhost/token", "192.168.1.5:5000", true)]
        [DataRow("http://localhost/token", "192.168.1.5:5000", true)]
        // Lookalike that isn't actually localhost: "localhost.example.com" is a public DNS
        // name, so the registry doesn't match the loopback exception either.
        [DataRow("http://localhost/token", "localhost.example.com:5000", true)]
        public void ValidateRealmUri_RejectsLoopbackDnsNameRealm(string realm, string registryName, bool isInsecureRegistry)
        {
            Assert.ThrowsExactly<InvalidAuthResponseException>(() =>
                AuthHandshakeMessageHandler.ValidateRealmUri(realm, registryName, isInsecureRegistry));
        }

        /// <summary>
        /// Verifies that a realm whose host is a reserved loopback DNS name is permitted
        /// when the registry is insecure and the registry host is itself loopback-equivalent
        /// (a loopback IP literal, <c>localhost</c>, or a <c>*.localhost</c> subdomain).
        /// Mirrors <see cref="ValidateRealmUri_AllowsMatchingIpLiteralWhenInsecure"/> for the
        /// case where the realm side uses a DNS name instead of an IP literal.
        /// </summary>
        [TestMethod]
        [DataRow("http://localhost:5000/auth", "localhost:5000")]
        [DataRow("https://localhost:5000/auth", "localhost:5000")]
        [DataRow("http://localhost:7000/auth", "localhost:5000")]           // port-independent
        [DataRow("http://foo.localhost:5000/auth", "localhost:5000")]       // *.localhost realm
        [DataRow("http://localhost:5000/auth", "registry.localhost:5000")]  // *.localhost registry
        [DataRow("http://localhost:5000/auth", "127.0.0.1:5000")]           // registry is loopback IP literal
        [DataRow("http://localhost:5000/auth", "[::1]:5000")]               // registry is IPv6 loopback literal
        public void ValidateRealmUri_AllowsLoopbackDnsNameRealm_WhenInsecureAndRegistryIsLoopback(string realm, string registryName)
        {
            Uri uri = AuthHandshakeMessageHandler.ValidateRealmUri(realm, registryName, isInsecureRegistry: true);
            Assert.AreEqual(realm, uri.AbsoluteUri);
        }

        /// <summary>
        /// Verifies that public DNS-named realms (i.e. not RFC 6761 loopback names and not
        /// IP literals) pass all guards regardless of the insecure flag - this is the
        /// expected shape for any production token endpoint.
        /// </summary>
        [TestMethod]
        [DataRow("https://auth.example.com/token", "registry.example.com", false)]
        [DataRow("https://auth.docker.io/token", "registry-1.docker.io", false)]    // real Docker Hub realm shape
        [DataRow("http://auth.example.com:8080/token", "registry.example.com:5000", true)]
        public void ValidateRealmUri_AllowsPublicDnsRealms(string realm, string registryName, bool isInsecureRegistry)
        {
            Uri uri = AuthHandshakeMessageHandler.ValidateRealmUri(realm, registryName, isInsecureRegistry);
            Assert.AreEqual(realm, uri.AbsoluteUri);
        }

        /// <summary>
        /// End-to-end verification that an invalid bearer realm in a 401 challenge causes
        /// <see cref="AuthHandshakeMessageHandler.SendAsync"/> to throw
        /// <see cref="InvalidAuthResponseException"/> and dispatch zero requests to the
        /// realm host.
        /// </summary>
        [TestMethod]
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
                new Uri(requestUrl),
                isInsecureRegistry: false,
                new ServerMessageHandler(Server),
                NullLogger.Instance,
                RegistryMode.Pull);
            using var httpClient = new HttpClient(authHandler);

            await Assert.ThrowsExactlyAsync<InvalidAuthResponseException>(() =>
                httpClient.GetAsync(requestUrl, TestContext.CancellationToken));

            // The handler must not have followed the malicious realm.
            Assert.AreEqual(0, tokenRequestCount);
        }
    }
}
