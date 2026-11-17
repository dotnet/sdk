// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Sockets;
using System.Web;
using System.Net.Http.Headers;
using System.Collections.Specialized;
using System.Collections.Concurrent;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.NET.Build.Containers.Tasks;
using Moq;

namespace Microsoft.NET.Build.Containers.UnitTests
{
    [TestClass]
    // Mutates process-global registry credentials and HTTP proxy settings.
    [DoNotParallelize]
    public class AuthHandshakeMessageHandlerTests
    {
        public TestContext TestContext { get; set; } = default!;

        private const string TestRegistryName = "registry.test";
        private const string RequestUrl = $"https://{TestRegistryName}/v2";
        private const string BearerRealmUrl = $"https://bearer.test/token";

        /// <summary>
        /// Verifies that each registry mode reads its supported credential environment
        /// variables, restoring the caller's environment after the scenario.
        /// </summary>
        [TestMethod]
        [DataRow("SDK_CONTAINER_REGISTRY_UNAME", "SDK_CONTAINER_REGISTRY_PWORD", (int)RegistryMode.Push)]
        [DataRow("DOTNET_CONTAINER_PUSH_REGISTRY_UNAME", "DOTNET_CONTAINER_PUSH_REGISTRY_PWORD", (int)RegistryMode.Push)]
        [DataRow("DOTNET_CONTAINER_PULL_REGISTRY_UNAME", "DOTNET_CONTAINER_PULL_REGISTRY_PWORD", (int)RegistryMode.Pull)]
        [DataRow("DOTNET_CONTAINER_PULL_REGISTRY_UNAME", "DOTNET_CONTAINER_PULL_REGISTRY_PWORD", (int)RegistryMode.PullFromOutput)]
        [DataRow("SDK_CONTAINER_REGISTRY_UNAME", "SDK_CONTAINER_REGISTRY_PWORD", (int)RegistryMode.PullFromOutput)]
        public void GetDockerCredentialsFromEnvironment_ReturnsCorrectValues(string unameVarName, string pwordVarName, int mode)
        {
            // Preserve the caller's values before supplying the identity for this mode.
            string? originalUnameValue = Environment.GetEnvironmentVariable(unameVarName);
            string? originalPwordValue = Environment.GetEnvironmentVariable(pwordVarName);

            Environment.SetEnvironmentVariable(unameVarName, "uname");
            Environment.SetEnvironmentVariable(pwordVarName, "pword");

            try
            {
                // Resolve credentials through the mode-specific lookup rather than reading the variables directly.
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

        /// <summary>
        /// Exercises file-based Basic, bearer-token, and OAuth authentication with the
        /// corresponding credential shapes and service/scope parameters.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(GetAuthenticateTestData))]
        public async Task Authenticate(string authConf, Func<HttpRequestMessage, HttpResponseMessage> server)
        {
            // Install the data row's credentials so its scripted server can validate the selected protocol.
            string authFile = Path.GetTempFileName();
            string? originalAuthFileValue = Environment.GetEnvironmentVariable("REGISTRY_AUTH_FILE");
            try
            {
                File.WriteAllText(authFile, authConf);
                Environment.SetEnvironmentVariable("REGISTRY_AUTH_FILE", authFile);

                var authHandler = new AuthHandshakeMessageHandler(
                    TestRegistryName,
                    new Uri(RequestUrl),
                    isInsecureRegistry: false,
                    new ServerMessageHandler(server),
                    NullLogger.Instance,
                    RegistryMode.Push,
                    // Serve token requests from the same fake server instead of the SSRF-protected network transport.
                    realmHandlerFactory: _ => new ServerMessageHandler(server));
                using var httpClient = new HttpClient(authHandler);

                // Complete the challenge and authenticated retry; the script checks headers and token parameters.
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
            foreach (string user in new[] { username, "<token>" })
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
                ServerWithOAuthForToken($"realm=\"{BearerRealmUrl}\", service=\"{service}\", scope=\"{scope}\"", BearerRealmUrl,
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
                ServerWithBasicAuthForToken($"realm=\"{BearerRealmUrl}\", service=\"{service}\", scope=\"{scope}\"", BearerRealmUrl, username, password,
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

                if (request.RequestUri?.GetLeftPart(UriPartial.Path) == new Uri(requestUri).GetLeftPart(UriPartial.Path))
                {
                    if (method == HttpMethod.Post && request.Method == HttpMethod.Get)
                    {
                        return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
                    }

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

        static string CreateLoopbackResponse(
            HttpStatusCode statusCode,
            Uri? location = null,
            string? authenticate = null)
        {
            var response = new StringBuilder()
                .Append($"HTTP/1.1 {(int)statusCode} Test\r\n")
                .Append("Content-Length: 0\r\n")
                .Append("Connection: close\r\n");

            if (location is not null)
            {
                response.Append($"Location: {location.AbsoluteUri}\r\n");
            }

            if (authenticate is not null)
            {
                response.Append($"WWW-Authenticate: {authenticate}\r\n");
            }

            return response.Append("\r\n").ToString();
        }

        private sealed class NoProxyScope : IDisposable
        {
            private readonly IWebProxy _originalProxy = HttpClient.DefaultProxy;

            public NoProxyScope()
            {
                HttpClient.DefaultProxy = new WebProxy();
            }

            public void Dispose()
            {
                HttpClient.DefaultProxy = _originalProxy;
            }
        }

        private sealed class LoopbackAuthRealmScope : IDisposable
        {
            private readonly string? _originalAllowedAddresses =
                Environment.GetEnvironmentVariable(RegistrySettings.EnvVariables.AuthRealmAllowedAddresses);

            public LoopbackAuthRealmScope()
            {
                Environment.SetEnvironmentVariable(
                    RegistrySettings.EnvVariables.AuthRealmAllowedAddresses,
                    "127.0.0.0/8;::1");
            }

            public void Dispose()
            {
                Environment.SetEnvironmentVariable(
                    RegistrySettings.EnvVariables.AuthRealmAllowedAddresses,
                    _originalAllowedAddresses);
            }
        }

        private sealed class RegistryAuthFileScope : IDisposable
        {
            private readonly string? _originalAuthFileValue = Environment.GetEnvironmentVariable("REGISTRY_AUTH_FILE");
            private readonly string? _originalUsername = Environment.GetEnvironmentVariable(ContainerHelpers.PullHostObjectUser);
            private readonly string? _originalPassword = Environment.GetEnvironmentVariable(ContainerHelpers.PullHostObjectPass);
            private readonly string _authFile = Path.GetTempFileName();

            public RegistryAuthFileScope(string json)
            {
                try
                {
                    File.WriteAllText(_authFile, json);
                    Environment.SetEnvironmentVariable("REGISTRY_AUTH_FILE", _authFile);
                    Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectUser, null);
                    Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectPass, null);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public void Dispose()
            {
                Environment.SetEnvironmentVariable("REGISTRY_AUTH_FILE", _originalAuthFileValue);
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectUser, _originalUsername);
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectPass, _originalPassword);
                File.Delete(_authFile);
            }
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

        /// <summary>
        /// Verifies invalid auth realm settings encountered while pulling the base image are logged
        /// by the MSBuild task boundary as their specific container diagnostics.
        /// </summary>
        [TestMethod]
        [DataRow(true, "CONTAINER2034")]
        [DataRow(false, "CONTAINER2033")]
        public async Task CreateNewImage_ReportsInvalidAuthRealmConfiguration(bool invalidPolicy, string expectedDiagnosticCode)
        {
            using TcpListener registryListener = new(IPAddress.Loopback, 0);
            registryListener.Start();
            string registryName = $"127.0.0.1:{((IPEndPoint)registryListener.LocalEndpoint).Port}";
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            string runtimeGraphPath = Path.GetTempFileName();
            string? originalInsecureRegistries = Environment.GetEnvironmentVariable(RegistrySettings.EnvVariables.InsecureRegistries);
            string? originalPolicy = Environment.GetEnvironmentVariable(RegistrySettings.EnvVariables.AuthRealmPolicy);
            string? originalAllowedAddresses = Environment.GetEnvironmentVariable(RegistrySettings.EnvVariables.AuthRealmAllowedAddresses);
            try
            {
                Environment.SetEnvironmentVariable(RegistrySettings.EnvVariables.InsecureRegistries, registryName);
                Environment.SetEnvironmentVariable(RegistrySettings.EnvVariables.AuthRealmPolicy, invalidPolicy ? "Strict" : null);
                Environment.SetEnvironmentVariable(RegistrySettings.EnvVariables.AuthRealmAllowedAddresses, invalidPolicy ? null : "not-an-ip");
                File.WriteAllText(runtimeGraphPath, "{}");

                Task<string> registryRequest = ServeRegistryChallengeAsync();
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
                using CancellationTokenRegistration cancellationRegistration = timeout.Token.Register(task.Cancel);

                bool succeeded = await Task.Run(task.Execute).WaitAsync(timeout.Token);
                await registryRequest;

                Assert.IsFalse(succeeded);
                Assert.HasCount(1, errors);
                Assert.AreEqual(expectedDiagnosticCode, errors[0].Code);
                Assert.DoesNotContain("MSB4018", errors[0].Message!);
            }
            finally
            {
                Environment.SetEnvironmentVariable(RegistrySettings.EnvVariables.InsecureRegistries, originalInsecureRegistries);
                Environment.SetEnvironmentVariable(RegistrySettings.EnvVariables.AuthRealmPolicy, originalPolicy);
                Environment.SetEnvironmentVariable(RegistrySettings.EnvVariables.AuthRealmAllowedAddresses, originalAllowedAddresses);
                File.Delete(runtimeGraphPath);
            }

            async Task<string> ServeRegistryChallengeAsync()
            {
                using (TcpClient client = await registryListener.AcceptTcpClientAsync(timeout.Token))
                {
                    using NetworkStream stream = client.GetStream();
                    await stream.ReadExactlyAsync(new byte[5], timeout.Token);
                    await stream.WriteAsync(
                        "HTTP/1.1 400 Bad Request\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8.ToArray(),
                        timeout.Token);
                }

                return await SendResponseAsync(
                    registryListener,
                    "HTTP/1.1 401 Unauthorized\r\n"
                        + "WWW-Authenticate: Bearer realm=\"https://auth.invalid/token\", service=\"registry\"\r\n"
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

        private sealed class AsyncServerMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _server;

            public AsyncServerMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> server)
            {
                _server = server;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => _server(request, cancellationToken);
        }

        private sealed class TrackingResponseMessage : HttpResponseMessage
        {
            public TrackingResponseMessage(HttpStatusCode statusCode)
                : base(statusCode)
            {
            }

            public int DisposeCount { get; private set; }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    DisposeCount++;
                }

                base.Dispose(disposing);
            }
        }

        private static Func<TimeSpan, CancellationToken, Task> CreateRecordingDelay(List<TimeSpan> delays) =>
            (duration, cancellationToken) =>
            {
                delays.Add(duration);
                return Task.Delay(TimeSpan.Zero, cancellationToken);
            };

        private sealed class FailAfterRedirectHandler(HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
        {
            public bool Failed { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
                if (!Failed && request.RequestUri?.AbsolutePath == "/redirected")
                {
                    // Fail after the real redirect handler has mutated the request and stripped Authorization.
                    Failed = true;
                    response.Dispose();
                    throw new HttpRequestException(
                        HttpRequestError.ConnectionError,
                        "Simulated redirected transport failure.",
                        new IOException("Simulated I/O failure.", new SocketException()));
                }

                return response;
            }
        }

        private sealed record LoopbackHttpRequest(string Method, string RequestTarget, string? Authorization, string Body);

        private sealed class LoopbackHttpServer : IAsyncDisposable
        {
            private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
            private readonly Func<string, string> _responseFactory;
            private readonly CancellationTokenSource _cancellation = new();
            private readonly ConcurrentQueue<LoopbackHttpRequest> _requests = new();
            private readonly Task _serverTask;
            private int _requestCount;
            private int _authorizationRequestCount;

            public LoopbackHttpServer(Func<string, string> responseFactory)
            {
                _responseFactory = responseFactory;
                _listener.Start();
                IPEndPoint endpoint = (IPEndPoint)_listener.LocalEndpoint;
                BaseUri = new Uri($"http://{endpoint.Address}:{endpoint.Port}/");
                _serverTask = ServeAsync();
            }

            public Uri BaseUri { get; }

            public int RequestCount => Volatile.Read(ref _requestCount);

            public int AuthorizationRequestCount => Volatile.Read(ref _authorizationRequestCount);

            public LoopbackHttpRequest[] Requests => _requests.ToArray();

            private async Task ServeAsync()
            {
                try
                {
                    while (true)
                    {
                        using TcpClient client = await _listener.AcceptTcpClientAsync(_cancellation.Token);
                        using NetworkStream stream = client.GetStream();
                        LoopbackHttpRequest request = await ReadRequestAsync(stream, _cancellation.Token);
                        _requests.Enqueue(request);
                        Interlocked.Increment(ref _requestCount);
                        if (request.Authorization is not null)
                        {
                            Interlocked.Increment(ref _authorizationRequestCount);
                        }
                        byte[] response = Encoding.ASCII.GetBytes(_responseFactory(request.RequestTarget));
                        await stream.WriteAsync(response, _cancellation.Token);
                    }
                }
                catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
                {
                }
            }

            private static async Task<LoopbackHttpRequest> ReadRequestAsync(Stream stream, CancellationToken cancellationToken)
            {
                var headerBytes = new List<byte>();
                byte[] singleByte = new byte[1];

                while (headerBytes.Count < 64 * 1024)
                {
                    int bytesRead = await stream.ReadAsync(singleByte, cancellationToken);
                    if (bytesRead == 0)
                    {
                        throw new IOException("The loopback client closed before sending complete HTTP headers.");
                    }

                    headerBytes.Add(singleByte[0]);
                    int count = headerBytes.Count;
                    if (count >= 4
                        && headerBytes[count - 4] == '\r'
                        && headerBytes[count - 3] == '\n'
                        && headerBytes[count - 2] == '\r'
                        && headerBytes[count - 1] == '\n')
                    {
                        break;
                    }
                }

                string headers = Encoding.ASCII.GetString(headerBytes.ToArray());
                string[] lines = headers.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
                string[] requestLine = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                int contentLength = lines
                    .Where(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    .Select(line => int.Parse(line["Content-Length:".Length..].Trim()))
                    .SingleOrDefault();

                byte[] body = new byte[contentLength];
                if (contentLength > 0)
                {
                    await stream.ReadExactlyAsync(body, cancellationToken);
                }

                string? authorization = lines
                    .Where(line => line.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase))
                    .Select(line => line["Authorization:".Length..].Trim())
                    .SingleOrDefault();
                return new LoopbackHttpRequest(requestLine[0], requestLine[1], authorization, Encoding.UTF8.GetString(body));
            }

            public async ValueTask DisposeAsync()
            {
                _cancellation.Cancel();
                try
                {
                    // Keep the listener active until the loop has observed cancellation.
                    await _serverTask;
                }
                finally
                {
                    _listener.Stop();
                    _cancellation.Dispose();
                }
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
        [DataRow("https://93.184.215.14/token", false)]         // public-routable IP literal, outside every blocked range, must be allowed
        [DataRow("http://auth.example.com/token", true)]        // downgrade permitted only when insecure
        public void ValidateRealmUri_AcceptsAllowedSchemes(string realm, bool isInsecureRegistry)
        {
            // Exercise the scheme policy with public hosts so IP-literal restrictions do not mask the result.
            Uri uri = AuthHandshakeMessageHandler.ValidateRealmUri(realm, "registry.example.com", isInsecureRegistry);

            // Accept the permitted endpoint without rewriting the advertised realm URL.
            Assert.AreEqual(realm, uri.AbsoluteUri);
        }

        /// <summary>
        /// Verifies that every scheme outside the allowlist is rejected: http on a secure
        /// registry, and any non-http(s) scheme regardless of the insecure flag. Defends
        /// against credential downgrade and transmission over unsupported transports.
        /// </summary>
        [TestMethod]
        [DataRow("http://auth.example.com/token", false)] // http on secure registry
        [DataRow("ftp://auth.example.com/token", false)]  // unsupported scheme on secure registry
        [DataRow("ftp://auth.example.com/token", true)]   // unsupported scheme stays rejected even when insecure
        [DataRow("file:///etc/passwd", false)]            // file scheme
        public void ValidateRealmUri_RejectsDisallowedSchemes(string realm, bool isInsecureRegistry)
        {
            // Reject the forbidden scheme before any credential-bearing request can be created.
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
        /// End-to-end verification with the default token transport and policy (Public) that a
        /// bearer realm resolving to an internal or special-use address makes
        /// <see cref="AuthHandshakeMessageHandler.SendAsync"/> throw <see cref="InvalidAuthResponseException"/>
        /// that names the configuration variables. The connection is refused before it is opened.
        /// </summary>
        [TestMethod]
        [DataRow("https://169.254.169.254/token")]
        [DataRow("https://[fe80::1]/token")]
        [DataRow("https://10.0.0.7/token")]
        [DataRow("https://127.0.0.1/token")]
        [DataRow("https://localhost/token")]
        [DataRow("https://[::ffff:127.0.0.1]/token")]
        [DataRow("https://[::ffff:169.254.169.254]/token")]
        [DataRow("https://127\uFF0E0\uFF0E0\uFF0E1/token")]
        [DataRow("https://169\uFF0E254\uFF0E169\uFF0E254/token")]
        [DataRow("https://10\uFF0E0\uFF0E0\uFF0E5/token")]
        [DataRow("https://127\u30020\u30020\u30021/token")]
        public async Task SendAsync_ThrowsOnInternalBearerRealm_WithDefaultPolicy(string realm)
        {
            // Use a unique registry name so the scenario is isolated from other authentication tests.
            string registryName = $"realm-policy-test-{Guid.NewGuid():N}.invalid";
            string requestUrl = $"https://{registryName}/v2";
            int requestCount = 0;
            HttpResponseMessage Server(HttpRequestMessage request)
            {
                requestCount++;
                var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
                response.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue("Bearer", $"realm=\"{realm}\", service=\"evil\""));
                return response;
            }

            InvalidAuthResponseException exception = await WithRegistryCredentialsAsync(registryName, async () =>
            {
                var authHandler = new AuthHandshakeMessageHandler(
                    registryName,
                    new Uri(requestUrl),
                    isInsecureRegistry: false,
                    new ServerMessageHandler(Server),
                    NullLogger.Instance,
                    RegistryMode.Pull);
                using var httpClient = new HttpClient(authHandler);

                return await Assert.ThrowsExactlyAsync<InvalidAuthResponseException>(() =>
                    httpClient.GetAsync(requestUrl, TestContext.CancellationToken));
            });

            Assert.Contains("CONTAINER1019", exception.Message);
            Assert.Contains(RegistrySettings.EnvVariables.AuthRealmAllowedAddresses, exception.Message);
            Assert.Contains(RegistrySettings.EnvVariables.AuthRealmPolicy, exception.Message);
            // The token request is never sent through the registry transport.
            Assert.AreEqual(1, requestCount);
        }

        /// <summary>
        /// Verifies that a realm on an allowed address gets the token request through the
        /// SSRF-protected transport rather than the registry transport.
        /// </summary>
        [TestMethod]
        public async Task SendAsync_RequestsTokenFromAllowedRealmThroughSsrfProtectedTransport()
        {
            using TcpListener realmListener = new(IPAddress.Loopback, 0);
            realmListener.Start();
            int realmPort = ((IPEndPoint)realmListener.LocalEndpoint).Port;
            string registryName = $"127.0.0.1:{realmPort}";
            Uri registryUri = new($"https://{registryName}/v2/");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));

            const string token = "realm-token";
            string tokenJson = $$"""{"token":"{{token}}"}""";
            Task<string> realmRequest = SendResponseAsync(
                realmListener,
                "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\n"
                    + $"Content-Length: {tokenJson.Length}\r\nConnection: close\r\n\r\n{tokenJson}",
                timeout.Token,
                stopListenerAfterResponse: true);

            HttpResponseMessage Server(HttpRequestMessage request)
            {
                // The registry transport must only see registry requests.
                Assert.AreEqual(registryUri, request.RequestUri);
                if (request.Headers.Authorization is { Scheme: "Bearer", Parameter: token })
                {
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }

                return CreateRequestAuthenticateResponse("Bearer", $"realm=\"http://localhost:{realmPort}/token\"");
            }

            var environment = new Mock<IEnvironmentProvider>();
            environment
                .Setup(e => e.GetEnvironmentVariable(RegistrySettings.EnvVariables.AuthRealmAllowedAddresses))
                .Returns("127.0.0.0/8;::1");

            await WithRegistryCredentialsAsync(registryName, async () =>
            {
                var authHandler = new AuthHandshakeMessageHandler(
                    registryName,
                    registryUri,
                    isInsecureRegistry: true,
                    new ServerMessageHandler(Server),
                    NullLogger.Instance,
                    RegistryMode.Push,
                    new AuthRealmHandlerFactory(registryName, registryUri, isInsecureRegistry: true, environment.Object).Create);
                using var httpClient = new HttpClient(authHandler);

                using HttpResponseMessage response = await httpClient.GetAsync(registryUri, timeout.Token);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            });

            string realmRequestHeaders = await realmRequest;
            Assert.StartsWith("GET /token", realmRequestHeaders);
            Assert.Contains("Authorization: Basic ", realmRequestHeaders);
        }

        /// <summary>
        /// Exercises identity-token authentication through the production registry and AntiSSRF
        /// transports, verifying that a 307/308 cannot forward the refresh-token form to another origin.
        /// </summary>
        [TestMethod]
        [DataRow((int)HttpStatusCode.TemporaryRedirect)]
        [DataRow((int)HttpStatusCode.PermanentRedirect)]
        public async Task OAuthCredentialsAreNotForwardedThroughRedirect(int redirectStatusCode)
        {
            // Explicitly allow local realms so AntiSSRF cannot mask the redirect protection.
            using var proxyScope = new NoProxyScope();
            using var realmScope = new LoopbackAuthRealmScope();
            await using var redirectedServer = new LoopbackHttpServer(
                _ => CreateLoopbackResponse(HttpStatusCode.OK));
            await using var authenticationServer = new LoopbackHttpServer(
                _ => CreateLoopbackResponse(
                    (HttpStatusCode)redirectStatusCode,
                    new Uri(redirectedServer.BaseUri, "token")));
            await using var registryServer = new LoopbackHttpServer(
                _ => CreateLoopbackResponse(
                    HttpStatusCode.Unauthorized,
                    authenticate: $"Bearer realm=\"{new Uri(authenticationServer.BaseUri, "token").AbsoluteUri}\""));

            // Select the identity-token flow without allowing inherited pull credentials to override it.
            string registryName = registryServer.BaseUri.Authority;
            string identityToken = "identity-token";

            using var authFile = new RegistryAuthFileScope(
                $$"""
                {
                    "auths": {
                        "{{registryName}}": {
                            "identitytoken": "{{identityToken}}",
                            "auth": "{{GetUserPasswordBase64("__", "__")}}"
                        }
                    }
                }
                """);

            // Let a real registry challenge dispatch the credential-bearing POST through the real realm factory.
            using HttpClient httpClient = DefaultRegistryAPI.CreateClient(
                registryName,
                registryServer.BaseUri,
                NullLogger.Instance,
                isInsecureRegistry: true,
                RegistryMode.Pull);

            // A redirect is a failed token acquisition, not permission to send credentials to another endpoint.
            await Assert.ThrowsExactlyAsync<UnableToAccessRepositoryException>(() =>
                httpClient.GetAsync(new Uri(registryServer.BaseUri, "v2"), TestContext.CancellationToken));

            // Verify the secret reached the original realm in its form, but the redirect sink remained untouched.
            Assert.AreEqual(1, registryServer.RequestCount);
            LoopbackHttpRequest[] requests = authenticationServer.Requests;
            Assert.HasCount(1, requests);
            Assert.AreEqual("POST", requests[0].Method);
            Assert.AreEqual("/token", requests[0].RequestTarget);
            Assert.IsNull(requests[0].Authorization);
            NameValueCollection parameters = HttpUtility.ParseQueryString(requests[0].Body);
            Assert.AreEqual("refresh_token", parameters["grant_type"]);
            Assert.AreEqual(identityToken, parameters["refresh_token"]);
            Assert.AreEqual(0, redirectedServer.RequestCount);
        }

        /// <summary>
        /// Exercises the production AntiSSRF transport with username/password credentials.
        /// A redirected or rejected Basic token GET falls back to a password-grant POST, whose
        /// 307/308 response must not forward the credential form to the allowed redirect sink.
        /// </summary>
        [TestMethod]
        [DataRow(307, false)]
        [DataRow(308, false)]
        [DataRow(307, true)]
        [DataRow(308, true)]
        public async Task UsernamePasswordCredentialsAreNotForwardedThroughRedirect(int redirectStatusCode, bool redirectTokenGet)
        {
            // Allow both local endpoints so a zero sink count proves redirect rejection, not an address-policy failure.
            using var proxyScope = new NoProxyScope();
            using var realmScope = new LoopbackAuthRealmScope();
            await using var redirectedServer = new LoopbackHttpServer(
                _ => CreateLoopbackResponse(HttpStatusCode.OK));
            int authenticationRequestCount = 0;
            await using var authenticationServer = new LoopbackHttpServer(_ =>
                ++authenticationRequestCount == 1 && !redirectTokenGet
                    ? CreateLoopbackResponse(HttpStatusCode.MethodNotAllowed)
                    : CreateLoopbackResponse(
                        (HttpStatusCode)redirectStatusCode,
                        new Uri(redirectedServer.BaseUri, "token")));
            await using var registryServer = new LoopbackHttpServer(
                _ => CreateLoopbackResponse(
                    HttpStatusCode.Unauthorized,
                    authenticate: $"Bearer realm=\"{new Uri(authenticationServer.BaseUri, "token").AbsoluteUri}\""));

            // Use a file-based username/password identity to exercise both token acquisition protocols.
            string registryName = registryServer.BaseUri.Authority;
            string username = "user";
            string password = "pass";
            using var authFile = new RegistryAuthFileScope(
                $$"""
                {
                    "auths": {
                        "{{registryName}}": {
                            "auth": "{{GetUserPasswordBase64(username, password)}}"
                        }
                    }
                }
                """);

            // Do not inject a fake realm handler: production AntiSSRF must enforce the redirect policy.
            using HttpClient httpClient = DefaultRegistryAPI.CreateClient(
                registryName,
                registryServer.BaseUri,
                NullLogger.Instance,
                isInsecureRegistry: true,
                RegistryMode.Pull);

            // A failed GET selects OAuth; its credential-bearing POST also returns a redirect.
            await Assert.ThrowsExactlyAsync<UnableToAccessRepositoryException>(() =>
                httpClient.GetAsync(new Uri(registryServer.BaseUri, "v2"), TestContext.CancellationToken));

            // The initial GET carries Basic auth, while the fallback POST carries credentials only in its form.
            Assert.AreEqual(1, registryServer.RequestCount);
            LoopbackHttpRequest[] requests = authenticationServer.Requests;
            Assert.HasCount(2, requests);
            Assert.AreEqual("GET", requests[0].Method);
            Assert.AreEqual("/token", requests[0].RequestTarget);
            Assert.AreEqual($"Basic {GetUserPasswordBase64(username, password)}", requests[0].Authorization);
            Assert.AreEqual("", requests[0].Body);
            Assert.AreEqual("POST", requests[1].Method);
            Assert.AreEqual("/token", requests[1].RequestTarget);
            Assert.IsNull(requests[1].Authorization);
            NameValueCollection parameters = HttpUtility.ParseQueryString(requests[1].Body);
            Assert.AreEqual("password", parameters["grant_type"]);
            Assert.AreEqual(username, parameters["username"]);
            Assert.AreEqual(password, parameters["password"]);
            Assert.AreEqual(0, redirectedServer.RequestCount);
        }

        /// <summary>
        /// Exercises the real authentication transport with token-endpoint 3xx responses,
        /// proving that neither same-origin nor cross-origin Location URIs are followed.
        /// </summary>
        [TestMethod]
        [DataRow(300, false)]
        [DataRow(301, false)]
        [DataRow(302, false)]
        [DataRow(303, false)]
        [DataRow(307, false)]
        [DataRow(308, false)]
        [DataRow(300, true)]
        [DataRow(301, true)]
        [DataRow(302, true)]
        [DataRow(303, true)]
        [DataRow(307, true)]
        [DataRow(308, true)]
        public async Task ProductionAuthenticationTransportDoesNotFollowRedirects(int statusCode, bool sameOrigin)
        {
            // Separate the registry, token endpoint, and redirect sink while bypassing inherited proxies.
            using var proxyScope = new NoProxyScope();
            using var realmScope = new LoopbackAuthRealmScope();
            await using var redirectedServer = new LoopbackHttpServer(
                _ => CreateLoopbackResponse(HttpStatusCode.OK));

            Uri redirectedUri = new(redirectedServer.BaseUri, "redirected");
            await using var authenticationServer = new LoopbackHttpServer(
                requestTarget => requestTarget == "/token"
                    ? CreateLoopbackResponse((HttpStatusCode)statusCode, redirectedUri)
                    : CreateLoopbackResponse(HttpStatusCode.OK));

            if (sameOrigin)
            {
                // A different path on the token server must be rejected just like a different authority.
                redirectedUri = new Uri(authenticationServer.BaseUri, "redirected");
            }

            await using var registryServer = new LoopbackHttpServer(
                _ => CreateLoopbackResponse(
                    HttpStatusCode.Unauthorized,
                    authenticate: $"Bearer realm=\"{new Uri(authenticationServer.BaseUri, "token").AbsoluteUri}\""));

            string registryName = registryServer.BaseUri.Authority;
            using var authFile = new RegistryAuthFileScope(
                $$"""
                {
                    "auths": {
                        "{{registryName}}": {
                            "identitytoken": "identity-token",
                            "auth": "{{GetUserPasswordBase64("__", "__")}}"
                        }
                    }
                }
                """);

            // Use production handler composition, not a fake transport's interpretation of redirects.
            using HttpClient httpClient = DefaultRegistryAPI.CreateClient(
                registryName,
                registryServer.BaseUri,
                NullLogger.Instance,
                isInsecureRegistry: true,
                RegistryMode.Pull);

            // Trigger the credential-bearing POST through an actual registry challenge.
            await Assert.ThrowsExactlyAsync<UnableToAccessRepositoryException>(() =>
                httpClient.GetAsync(new Uri(registryServer.BaseUri, "v2"), TestContext.CancellationToken));

            // A followed Location would add a second token-server request or reach the separate sink.
            Assert.AreEqual(1, registryServer.RequestCount);
            Assert.AreEqual(1, authenticationServer.RequestCount);
            Assert.AreEqual(0, redirectedServer.RequestCount);
        }

        /// <summary>
        /// Models Docker Hub's alias with docker.io credentials and a different loopback
        /// transport origin, verifying that the production client authenticates against
        /// its effective base URI rather than comparing requests to the credential name.
        /// </summary>
        [TestMethod]
        public async Task ProductionAuthenticationUsesEffectiveRegistryOrigin()
        {
            // Keep the production transport local while retaining Docker Hub's credential lookup name.
            using var proxyScope = new NoProxyScope();
            const string RegistryName = "docker.io";
            int registryRequestCount = 0;
            await using var registryServer = new LoopbackHttpServer(_ =>
                Interlocked.Increment(ref registryRequestCount) == 1
                    ? CreateLoopbackResponse(HttpStatusCode.Unauthorized, authenticate: "Basic")
                    : CreateLoopbackResponse(HttpStatusCode.OK));
            using var authFile = new RegistryAuthFileScope(
                $$"""
                {
                    "auths": {
                        "https://index.docker.io/v1/": {
                            "auth": "{{GetUserPasswordBase64("user", "pass")}}"
                        }
                    }
                }
                """);

            // Use the real client factory so omitting its effective-origin argument breaks the scenario.
            using HttpClient httpClient = DefaultRegistryAPI.CreateClient(
                RegistryName,
                registryServer.BaseUri,
                NullLogger.Instance,
                isInsecureRegistry: true,
                RegistryMode.Pull);

            // The initial challenge must trigger a credential-bearing retry despite the differing names.
            using HttpResponseMessage response = await httpClient.GetAsync(
                new Uri(registryServer.BaseUri, "v2"),
                TestContext.CancellationToken);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(2, registryServer.RequestCount);
            Assert.AreEqual(1, registryServer.AuthorizationRequestCount);
        }

        /// <summary>
        /// Verifies that disabling authentication redirects does not disable legitimate
        /// redirects on the separate production registry transport.
        /// </summary>
        [TestMethod]
        public async Task ProductionRegistryTransportFollowsRedirects()
        {
            // Redirect a normal registry request to another local server without involving authentication.
            using var proxyScope = new NoProxyScope();
            await using var redirectedServer = new LoopbackHttpServer(
                _ => CreateLoopbackResponse(HttpStatusCode.OK));
            await using var registryServer = new LoopbackHttpServer(
                _ => CreateLoopbackResponse(
                    HttpStatusCode.TemporaryRedirect,
                    new Uri(redirectedServer.BaseUri, "redirected")));

            using HttpClient httpClient = DefaultRegistryAPI.CreateClient(
                registryServer.BaseUri.Authority,
                registryServer.BaseUri,
                NullLogger.Instance,
                isInsecureRegistry: true,
                RegistryMode.Pull);

            // Exercise the production redirect policy rather than manually following Location.
            using HttpResponseMessage response = await httpClient.GetAsync(
                new Uri(registryServer.BaseUri, "v2"),
                TestContext.CancellationToken);

            // Both servers must be visited exactly once to produce the final response.
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(1, registryServer.RequestCount);
            Assert.AreEqual(1, redirectedServer.RequestCount);
        }

        /// <summary>
        /// Follows a real registry redirect, injects a retryable failure after Authorization
        /// is stripped, and verifies that retries restore acquired or cached credentials only
        /// for the current registry origin, never for a cross-origin redirect target.
        /// </summary>
        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task RedirectedTransportRetryRestoresAuthenticationOnlyForRegistryOrigin(bool crossOrigin, bool cached)
        {
            // Use real redirect handling and synthetic credentials, with a separate cross-origin sink.
            using var proxyScope = new NoProxyScope();
            await using var redirectedServer = new LoopbackHttpServer(_ => CreateLoopbackResponse(HttpStatusCode.OK));
            int initialRequests = 0;
            int warmupRequests = 0;
            int tokenRequests = 0;
            var retryDelays = new List<TimeSpan>();
            Uri? redirectUri = null;
            const string Challenge = "Bearer realm=\"https://auth.test/token\"";
            await using var registryServer = new LoopbackHttpServer(path =>
            {
                if (path == "/warmup")
                {
                    return ++warmupRequests == 1
                        ? CreateLoopbackResponse(HttpStatusCode.Unauthorized, authenticate: Challenge)
                        : CreateLoopbackResponse(HttpStatusCode.OK);
                }

                if (path == "/v2")
                {
                    return ++initialRequests == 1 && !cached
                        ? CreateLoopbackResponse(HttpStatusCode.Unauthorized, authenticate: Challenge)
                        : CreateLoopbackResponse(HttpStatusCode.TemporaryRedirect, redirectUri);
                }

                return CreateLoopbackResponse(HttpStatusCode.OK);
            });
            redirectUri = new Uri(crossOrigin ? redirectedServer.BaseUri : registryServer.BaseUri, "redirected");
            string registryName = registryServer.BaseUri.Authority;
            using var authFile = new RegistryAuthFileScope(
                $$"""
                { "auths": { "{{registryName}}": { "auth": "{{GetUserPasswordBase64("user", "pass")}}" } } }
                """);
            var transport = new FailAfterRedirectHandler(new SocketsHttpHandler { UseCookies = false });
            using var httpClient = new HttpClient(new AuthHandshakeMessageHandler(
                registryName, registryServer.BaseUri, true, transport, NullLogger.Instance, RegistryMode.Pull,
                realmHandlerFactory: _ => new ServerMessageHandler(_ =>
                {
                    tokenRequests++;
                    return CreateBearerTokenResponse("registry-token");
                }),
                delay: CreateRecordingDelay(retryDelays)));

            // Optionally prime the handler cache before exercising the redirected failing request.
            if (cached)
            {
                using HttpResponseMessage warmup = await httpClient.GetAsync(
                    new Uri(registryServer.BaseUri, "warmup"), TestContext.CancellationToken);
                Assert.AreEqual(HttpStatusCode.OK, warmup.StatusCode);
            }

            // The retry must reuse the same acquisition without starting another protocol cycle.
            using HttpResponseMessage response = await httpClient.GetAsync(
                new Uri(registryServer.BaseUri, "v2"), TestContext.CancellationToken);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsTrue(transport.Failed);
            Assert.AreEqual(1, tokenRequests);
            Assert.AreSequenceEqual(new[] { TimeSpan.FromSeconds(2) }, retryDelays);

            // The first redirected send is stripped by .NET; only a same-origin retry may regain the token.
            LoopbackHttpRequest[] requests = (crossOrigin ? redirectedServer : registryServer).Requests
                .Where(request => request.RequestTarget == "/redirected").ToArray();
            Assert.HasCount(2, requests);
            Assert.IsNull(requests[0].Authorization);
            Assert.AreEqual(crossOrigin ? null : "Bearer registry-token", requests[1].Authorization);
        }

        /// <summary>
        /// Verifies that a registry redirect ending in a cross-origin 401 cannot use the
        /// configured registry's credentials to authenticate against the advertised realm.
        /// </summary>
        [TestMethod]
        public async Task ProductionRegistryRedirectToCrossOriginChallengeDoesNotAuthenticate()
        {
            // Make the redirect target advertise a token endpoint that records any forbidden authentication.
            using var proxyScope = new NoProxyScope();
            await using var authenticationServer = new LoopbackHttpServer(
                _ => CreateLoopbackResponse(HttpStatusCode.Unauthorized));
            await using var redirectedServer = new LoopbackHttpServer(
                _ => CreateLoopbackResponse(
                    HttpStatusCode.Unauthorized,
                    authenticate: "Bearer realm=\"" + new Uri(authenticationServer.BaseUri, "token").AbsoluteUri + "\""));
            await using var registryServer = new LoopbackHttpServer(
                _ => CreateLoopbackResponse(
                    HttpStatusCode.TemporaryRedirect,
                    new Uri(redirectedServer.BaseUri, "redirected")));

            string? originalUsername = Environment.GetEnvironmentVariable(ContainerHelpers.PullHostObjectUser);
            string? originalPassword = Environment.GetEnvironmentVariable(ContainerHelpers.PullHostObjectPass);

            try
            {
                // Supply usable credentials so zero token requests proves origin rejection, not missing credentials.
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectUser, "user");
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectPass, "pass");

                using HttpClient httpClient = DefaultRegistryAPI.CreateClient(
                    registryServer.BaseUri.Authority,
                    registryServer.BaseUri,
                    NullLogger.Instance,
                    isInsecureRegistry: true,
                    RegistryMode.Pull);

                // Follow the registry redirect, then reject the cross-origin authentication challenge.
                await Assert.ThrowsExactlyAsync<InvalidAuthResponseException>(() => httpClient.GetAsync(
                    new Uri(registryServer.BaseUri, "v2"),
                    TestContext.CancellationToken));

                // The cross-origin challenge must fail before contacting its advertised token server.
                Assert.AreEqual(1, registryServer.RequestCount);
                Assert.AreEqual(1, redirectedServer.RequestCount);
                Assert.AreEqual(0, authenticationServer.RequestCount);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectUser, originalUsername);
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectPass, originalPassword);
            }
        }

        /// <summary>
        /// Verifies that a request originating outside the registry stays unauthenticated
        /// even when a redirect ends at the registry with a valid Basic challenge.
        /// </summary>
        [TestMethod]
        public async Task ProductionCrossOriginRedirectToRegistryChallengeDoesNotAuthenticateOriginalRequest()
        {
            // Start at an external upload origin and redirect back to a registry that requires authentication.
            using var proxyScope = new NoProxyScope();
            await using var registryServer = new LoopbackHttpServer(
                _ => CreateLoopbackResponse(
                    HttpStatusCode.Unauthorized,
                    authenticate: "Basic"));
            await using var redirectedServer = new LoopbackHttpServer(
                _ => CreateLoopbackResponse(
                    HttpStatusCode.TemporaryRedirect,
                    new Uri(registryServer.BaseUri, "v2")));

            string? originalUsername = Environment.GetEnvironmentVariable(ContainerHelpers.PullHostObjectUser);
            string? originalPassword = Environment.GetEnvironmentVariable(ContainerHelpers.PullHostObjectPass);

            try
            {
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectUser, "user");
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectPass, "pass");

                using HttpClient httpClient = DefaultRegistryAPI.CreateClient(
                    registryServer.BaseUri.Authority,
                    registryServer.BaseUri,
                    NullLogger.Instance,
                    isInsecureRegistry: true,
                    RegistryMode.Pull);

                // The original origin must still control eligibility after HttpClient processes the redirect.
                using HttpResponseMessage response = await httpClient.GetAsync(
                    new Uri(redirectedServer.BaseUri, "upload"),
                    TestContext.CancellationToken);

                // No credentials may reach the original server, and no authenticated registry retry may occur.
                Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
                Assert.AreEqual(1, redirectedServer.RequestCount);
                Assert.AreEqual(0, redirectedServer.AuthorizationRequestCount);
                Assert.AreEqual(1, registryServer.RequestCount);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectUser, originalUsername);
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectPass, originalPassword);
            }
        }

        /// <summary>
        /// Verifies that a successful token is reused within one handler but is neither
        /// inherited nor overwritten by another handler for the same registry using
        /// different credentials.
        /// </summary>
        [TestMethod]
        public async Task SuccessfulAuthenticationIsCachedOnlyWithinHandlerInstance()
        {
            string registryName = $"cache-isolation-test-{Guid.NewGuid():N}.invalid";
            string requestUrl = $"https://{registryName}/v2";
            string realmUrl = "https://auth.test/token";
            var registryAuthorizations = new List<string?>();
            var authenticationAuthorizations = new List<string?>();

            // Accept either issued token so an incorrectly shared cache cannot hide behind reauthentication.
            HttpResponseMessage RegistryServer(HttpRequestMessage request)
            {
                Assert.AreEqual(registryName, request.RequestUri?.Host);
                string? token = request.Headers.Authorization?.Parameter;
                registryAuthorizations.Add(token);
                return token is "token-1" or "token-2"
                    ? new HttpResponseMessage(HttpStatusCode.OK)
                    : CreateRequestAuthenticateResponse("Bearer", $"realm=\"{realmUrl}\"");
            }

            // Issue distinct tokens and record which identity performed each handshake.
            HttpResponseMessage AuthenticationServer(HttpRequestMessage request)
            {
                Assert.AreEqual(realmUrl, request.RequestUri?.ToString());
                Assert.AreEqual(HttpMethod.Get, request.Method);
                Assert.AreEqual("Basic", request.Headers.Authorization?.Scheme);
                authenticationAuthorizations.Add(request.Headers.Authorization?.Parameter);
                return CreateBearerTokenResponse($"token-{authenticationAuthorizations.Count}");
            }

            HttpClient CreateClient() => new(
                new AuthHandshakeMessageHandler(
                    registryName,
                    new Uri(requestUrl),
                    isInsecureRegistry: false,
                    new ServerMessageHandler(RegistryServer),
                    NullLogger.Instance,
                    RegistryMode.Pull,
                    realmHandlerFactory: _ => new ServerMessageHandler(AuthenticationServer)));

            // Prime one handler with the first identity, then require its next request to reuse that token.
            using var firstAuthFile = new RegistryAuthFileScope(
                $$"""
                {
                    "auths": {
                        "{{registryName}}": {
                            "auth": "{{GetUserPasswordBase64("first-user", "first-password")}}"
                        }
                    }
                }
                """);
            using HttpClient firstClient = CreateClient();
            using HttpResponseMessage firstResponse = await firstClient.GetAsync(requestUrl, TestContext.CancellationToken);
            using HttpResponseMessage repeatedResponse = await firstClient.GetAsync(requestUrl, TestContext.CancellationToken);

            Assert.AreEqual(HttpStatusCode.OK, firstResponse.StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, repeatedResponse.StatusCode);
            Assert.HasCount(1, authenticationAuthorizations);
            Assert.AreSequenceEqual(new string?[] { null, "token-1", "token-1" }, registryAuthorizations);

            // Change the available credentials before creating another handler for the identical registry.
            using var secondAuthFile = new RegistryAuthFileScope(
                $$"""
                {
                    "auths": {
                        "{{registryName}}": {
                            "auth": "{{GetUserPasswordBase64("second-user", "second-password")}}"
                        }
                    }
                }
                """);
            using HttpClient secondClient = CreateClient();
            using HttpResponseMessage secondResponse = await secondClient.GetAsync(requestUrl, TestContext.CancellationToken);

            // Updating the second handler's cache must leave the first handler's accepted token intact.
            using HttpResponseMessage originalClientResponse = await firstClient.GetAsync(requestUrl, TestContext.CancellationToken);

            Assert.AreEqual(HttpStatusCode.OK, secondResponse.StatusCode);
            Assert.AreEqual(HttpStatusCode.OK, originalClientResponse.StatusCode);
            Assert.AreSequenceEqual(
                new string?[]
                {
                    GetUserPasswordBase64("first-user", "first-password"),
                    GetUserPasswordBase64("second-user", "second-password")
                },
                authenticationAuthorizations);
            Assert.AreSequenceEqual(
                new string?[] { null, "token-1", "token-1", null, "token-2", "token-1" },
                registryAuthorizations);
        }

        /// <summary>
        /// Primes a registry token and verifies that direct cross-origin requests neither
        /// receive the cached credential nor trigger authentication for their own challenges.
        /// </summary>
        [TestMethod]
        public async Task CachedAuthenticationIsNotSentToCrossOriginRequests()
        {
            // Model absolute upload URLs, which bypass HttpClient's automatic redirect credential stripping.
            string registryName = $"origin-test-{Guid.NewGuid():N}.invalid";
            string registryUrl = $"https://{registryName}/v2";
            string realmUrl = "https://auth.test/token";
            string? originalUsername = Environment.GetEnvironmentVariable(ContainerHelpers.PullHostObjectUser);
            string? originalPassword = Environment.GetEnvironmentVariable(ContainerHelpers.PullHostObjectPass);
            int authenticationRequestCount = 0;
            int crossOriginRequestCount = 0;

            try
            {
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectUser, "user");
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectPass, "pass");

                HttpResponseMessage RegistryServer(HttpRequestMessage request)
                {
                    if (request.RequestUri!.Host == registryName)
                    {
                        return request.Headers.Authorization?.Parameter == "registry-token"
                            ? new HttpResponseMessage(HttpStatusCode.OK)
                            : CreateRequestAuthenticateResponse("Bearer", $"realm=\"{realmUrl}\"");
                    }

                    Interlocked.Increment(ref crossOriginRequestCount);
                    Assert.IsNull(request.Headers.Authorization);
                    return request.RequestUri.AbsolutePath == "/challenge"
                        ? CreateRequestAuthenticateResponse("Bearer", "realm=\"https://other-auth.test/token\"")
                        : new HttpResponseMessage(HttpStatusCode.OK);
                }

                using var httpClient = new HttpClient(
                    new AuthHandshakeMessageHandler(
                        registryName,
                        new Uri(registryUrl),
                        isInsecureRegistry: false,
                        new ServerMessageHandler(RegistryServer),
                        NullLogger.Instance,
                        RegistryMode.Pull,
                        realmHandlerFactory: _ => new ServerMessageHandler(_ =>
                        {
                            Interlocked.Increment(ref authenticationRequestCount);
                            return CreateBearerTokenResponse("registry-token");
                        })));

                // Authenticate the registry first so subsequent requests have a cached credential to exclude.
                using HttpResponseMessage registryResponse = await httpClient.GetAsync(registryUrl, TestContext.CancellationToken);
                // Exercise both a successful external upload and an external 401 with a different realm.
                using HttpResponseMessage uploadResponse = await httpClient.GetAsync("https://uploads.test/chunk", TestContext.CancellationToken);
                await Assert.ThrowsExactlyAsync<InvalidAuthResponseException>(() =>
                    httpClient.GetAsync("https://uploads.test/challenge", TestContext.CancellationToken));

                // The two external requests must not receive the cache or create another token acquisition.
                Assert.AreEqual(HttpStatusCode.OK, registryResponse.StatusCode);
                Assert.AreEqual(HttpStatusCode.OK, uploadResponse.StatusCode);
                Assert.AreEqual(1, authenticationRequestCount);
                Assert.AreEqual(2, crossOriginRequestCount);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectUser, originalUsername);
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectPass, originalPassword);
            }
        }

        /// <summary>
        /// Verifies that insecure registries can authenticate at the HTTP fallback origin,
        /// using port 80 implicitly or preserving an explicitly configured registry port.
        /// </summary>
        [TestMethod]
        [DataRow("registry.test", "http://registry.test/v2")]
        [DataRow("registry.test:5000", "http://registry.test:5000/v2")]
        public async Task InsecureRegistryAuthenticationAppliesToFallbackOrigin(string registryName, string requestUrl)
        {
            // Present the URI after HTTPS-to-HTTP fallback while keeping the configured registry identity unchanged.
            string realmUrl = "http://auth.test/token";
            string? originalUsername = Environment.GetEnvironmentVariable(ContainerHelpers.PullHostObjectUser);
            string? originalPassword = Environment.GetEnvironmentVariable(ContainerHelpers.PullHostObjectPass);
            int authenticationRequestCount = 0;

            try
            {
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectUser, "user");
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectPass, "pass");

                using var httpClient = new HttpClient(
                    new AuthHandshakeMessageHandler(
                        registryName,
                        new Uri($"https://{registryName}/v2"),
                        isInsecureRegistry: true,
                        new ServerMessageHandler(request =>
                            request.Headers.Authorization?.Parameter == "registry-token"
                                ? new HttpResponseMessage(HttpStatusCode.OK)
                                : CreateRequestAuthenticateResponse("Bearer", $"realm=\"{realmUrl}\"")),
                        NullLogger.Instance,
                        RegistryMode.Pull,
                        realmHandlerFactory: _ => new ServerMessageHandler(_ =>
                        {
                            Interlocked.Increment(ref authenticationRequestCount);
                            return CreateBearerTokenResponse("registry-token");
                        })));

                // The origin check must permit this intentional fallback and complete exactly one handshake.
                using HttpResponseMessage response = await httpClient.GetAsync(requestUrl, TestContext.CancellationToken);

                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.AreEqual(1, authenticationRequestCount);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectUser, originalUsername);
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectPass, originalPassword);
            }
        }

        /// <summary>
        /// Verifies that the handler disposes the 401 consumed by authentication but leaves
        /// the terminal response alive until the caller disposes it.
        /// </summary>
        [TestMethod]
        public async Task SendAsyncDisposesConsumedUnauthorizedResponseButReturnsTerminalResponseUndisposed()
        {
            // Track separate initial and terminal 401 responses to distinguish handler and caller ownership.
            string registryName = $"response-disposal-test-{Guid.NewGuid():N}.invalid";
            string requestUrl = $"https://{registryName}/v2";
            string realmUrl = "https://auth.test/token";
            string? originalUsername = Environment.GetEnvironmentVariable(ContainerHelpers.PullHostObjectUser);
            string? originalPassword = Environment.GetEnvironmentVariable(ContainerHelpers.PullHostObjectPass);
            var consumedResponse = new TrackingResponseMessage(HttpStatusCode.Unauthorized);
            consumedResponse.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue("Bearer", $"realm=\"{realmUrl}\""));
            var terminalResponse = new TrackingResponseMessage(HttpStatusCode.Unauthorized);
            int registryRequestCount = 0;

            try
            {
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectUser, "user");
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectPass, "pass");

                using var httpClient = new HttpClient(
                    new AuthHandshakeMessageHandler(
                        registryName,
                        new Uri(requestUrl),
                        isInsecureRegistry: false,
                        new ServerMessageHandler(_ =>
                            Interlocked.Increment(ref registryRequestCount) == 1 ? consumedResponse : terminalResponse),
                        NullLogger.Instance,
                        RegistryMode.Pull,
                        realmHandlerFactory: _ => new ServerMessageHandler(_ => CreateBearerTokenResponse("token"))));

                // Consume the first challenge and return the retry's terminal response without another handshake.
                HttpResponseMessage response = await httpClient.GetAsync(requestUrl, TestContext.CancellationToken);

                // Only the response used internally for authentication should have been disposed.
                Assert.AreSame(terminalResponse, response);
                Assert.AreEqual(1, consumedResponse.DisposeCount);
                Assert.AreEqual(0, terminalResponse.DisposeCount);

                // The caller retains responsibility for releasing the returned response.
                response.Dispose();
                Assert.AreEqual(1, terminalResponse.DisposeCount);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectUser, originalUsername);
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectPass, originalPassword);
            }
        }

        /// <summary>
        /// Starts overlapping unauthenticated requests and verifies that they can each
        /// complete a handshake through an independently owned realm transport.
        /// </summary>
        [TestMethod]
        public async Task ConcurrentChallengesAuthenticateIndependentlyOverSharedTransport()
        {
            // Gate every initial request so none can populate the cache before the others reach the registry.
            const int RequestCount = 8;
            string registryName = $"concurrent-auth-test-{Guid.NewGuid():N}.invalid";
            string requestUrl = $"https://{registryName}/v2";
            string realmUrl = "https://auth.test/token";
            string? originalUsername = Environment.GetEnvironmentVariable(ContainerHelpers.PullHostObjectUser);
            string? originalPassword = Environment.GetEnvironmentVariable(ContainerHelpers.PullHostObjectPass);
            var allInitialRequestsReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int initialRequestCount = 0;
            int authenticationHandlerCount = 0;
            int authenticationRequestCount = 0;

            try
            {
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectUser, "user");
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectPass, "pass");

                async Task<HttpResponseMessage> RegistryServer(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    if (request.Headers.Authorization?.Scheme == "Bearer")
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK);
                    }

                    if (Interlocked.Increment(ref initialRequestCount) == RequestCount)
                    {
                        allInitialRequestsReceived.SetResult();
                    }

                    // Release all initial challenges together to force overlapping token acquisitions.
                    await allInitialRequestsReceived.Task.WaitAsync(cancellationToken);
                    return CreateRequestAuthenticateResponse("Bearer", $"realm=\"{realmUrl}\"");
                }

                HttpMessageHandler CreateAuthenticationHandler()
                {
                    Interlocked.Increment(ref authenticationHandlerCount);
                    return new AsyncServerMessageHandler((request, cancellationToken) =>
                    {
                        Interlocked.Increment(ref authenticationRequestCount);
                        return Task.FromResult(CreateBearerTokenResponse("token"));
                    });
                }

                using var httpClient = new HttpClient(
                    new AuthHandshakeMessageHandler(
                        registryName,
                        new Uri(requestUrl),
                        isInsecureRegistry: false,
                        new AsyncServerMessageHandler(RegistryServer),
                        NullLogger.Instance,
                        RegistryMode.Pull,
                        realmHandlerFactory: _ => CreateAuthenticationHandler()));

                // Drive real concurrent handler calls rather than sequential requests that could reuse a token.
                HttpResponseMessage[] responses = await Task.WhenAll(
                    Enumerable.Range(0, RequestCount)
                        .Select(_ => httpClient.GetAsync(requestUrl, TestContext.CancellationToken)));

                try
                {
                    // Each overlapping challenge gets its own disposable realm transport.
                    Assert.IsTrue(responses.All(response => response.StatusCode == HttpStatusCode.OK));
                    Assert.AreEqual(RequestCount, authenticationHandlerCount);
                    Assert.AreEqual(RequestCount, authenticationRequestCount);
                }
                finally
                {
                    // Each completed concurrent request still owns a response that must be released.
                    foreach (HttpResponseMessage response in responses)
                    {
                        response.Dispose();
                    }
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectUser, originalUsername);
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectPass, originalPassword);
            }
        }

        /// <summary>
        /// Rejects every issued token and verifies that each request performs at most one
        /// handshake and that a rejected candidate is not cached for the next request.
        /// </summary>
        [TestMethod]
        public async Task RejectedAuthenticationIsNotCachedAndEachRequestIsBounded()
        {
            // Issue distinguishable tokens while recording every registry attempt that rejects them.
            string registryName = $"bounded-auth-retry-test-{Guid.NewGuid():N}.invalid";
            string requestUrl = $"https://{registryName}/v2";
            string realmUrl = "https://auth.test/token";
            string? originalUsername = Environment.GetEnvironmentVariable(ContainerHelpers.PullHostObjectUser);
            string? originalPassword = Environment.GetEnvironmentVariable(ContainerHelpers.PullHostObjectPass);
            int authenticationHandlerCount = 0;
            int authenticationRequestCount = 0;
            var registryAuthorizations = new List<string?>();

            try
            {
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectUser, "user");
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectPass, "pass");

                HttpMessageHandler CreateAuthenticationHandler()
                {
                    Interlocked.Increment(ref authenticationHandlerCount);
                    return new ServerMessageHandler(request =>
                    {
                        int requestNumber = Interlocked.Increment(ref authenticationRequestCount);
                        return CreateBearerTokenResponse($"token-{requestNumber}");
                    });
                }

                using var httpClient = new HttpClient(
                    new AuthHandshakeMessageHandler(
                        registryName,
                        new Uri(requestUrl),
                        isInsecureRegistry: false,
                        new ServerMessageHandler(request =>
                        {
                            registryAuthorizations.Add(request.Headers.Authorization?.Parameter);
                            return CreateRequestAuthenticateResponse("Bearer", $"realm=\"{realmUrl}\"");
                        }),
                        NullLogger.Instance,
                        RegistryMode.Pull,
                        realmHandlerFactory: _ => CreateAuthenticationHandler()));

                // Two independent requests must each start unauthenticated and stop after their rejected retry.
                using HttpResponseMessage firstResponse = await httpClient.GetAsync(requestUrl, TestContext.CancellationToken);
                using HttpResponseMessage secondResponse = await httpClient.GetAsync(requestUrl, TestContext.CancellationToken);

                // The null entries prove that neither rejected token became a cached preemptive credential.
                Assert.AreEqual(HttpStatusCode.Unauthorized, firstResponse.StatusCode);
                Assert.AreEqual(HttpStatusCode.Unauthorized, secondResponse.StatusCode);
                Assert.AreEqual(2, authenticationHandlerCount);
                Assert.AreEqual(2, authenticationRequestCount);
                Assert.AreSequenceEqual(
                    new string?[] { null, "token-1", null, "token-2" },
                    registryAuthorizations);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectUser, originalUsername);
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectPass, originalPassword);
            }
        }

        /// <summary>
        /// Fails the first authenticated registry attempt with a retryable transport error,
        /// then rejects its retry, verifying that transport recovery cannot start another handshake.
        /// </summary>
        [TestMethod]
        public async Task RetryableFailureAfterAuthenticationDoesNotStartAnotherProtocolCycle()
        {
            // Make a second token succeed so an accidental extra handshake would produce the wrong final outcome.
            string registryName = $"auth-retry-test-{Guid.NewGuid():N}.invalid";
            string requestUrl = $"https://{registryName}/v2";
            string realmUrl = "https://auth.test/token";
            string? originalUsername = Environment.GetEnvironmentVariable(ContainerHelpers.PullHostObjectUser);
            string? originalPassword = Environment.GetEnvironmentVariable(ContainerHelpers.PullHostObjectPass);
            int tokenOneRequestCount = 0;
            int authenticationRequestCount = 0;
            var retryDelays = new List<TimeSpan>();

            try
            {
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectUser, "user");
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectPass, "pass");

                HttpResponseMessage RegistryServer(HttpRequestMessage request)
                {
                    if (request.Headers.Authorization?.Parameter == "token-2")
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK);
                    }

                    if (request.Headers.Authorization?.Parameter == "token-1"
                        && Interlocked.Increment(ref tokenOneRequestCount) == 1)
                    {
                        // Match the handler's retryable exception shape after the first token has already been acquired.
                        throw new HttpRequestException(
                            HttpRequestError.ConnectionError,
                            "Simulated connection failure.",
                            new IOException(
                                "Simulated I/O failure.",
                                new System.Net.Sockets.SocketException()));
                    }

                    return CreateRequestAuthenticateResponse("Bearer", $"realm=\"{realmUrl}\"");
                }

                HttpMessageHandler CreateAuthenticationHandler() =>
                    new ServerMessageHandler(request =>
                    {
                        int requestNumber = Interlocked.Increment(ref authenticationRequestCount);
                        return CreateBearerTokenResponse($"token-{requestNumber}");
                    });

                using var httpClient = new HttpClient(
                    new AuthHandshakeMessageHandler(
                        registryName,
                        new Uri(requestUrl),
                        isInsecureRegistry: false,
                        new ServerMessageHandler(RegistryServer),
                        NullLogger.Instance,
                        RegistryMode.Pull,
                        realmHandlerFactory: _ => CreateAuthenticationHandler(),
                        delay: CreateRecordingDelay(retryDelays)));

                // Recover the transport using the existing candidate, then return its rejection to the caller.
                using HttpResponseMessage response = await httpClient.GetAsync(requestUrl, TestContext.CancellationToken);

                // A second protocol cycle would obtain token-2 and incorrectly turn this response into success.
                Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
                Assert.AreEqual(1, authenticationRequestCount);
                Assert.AreSequenceEqual(new[] { TimeSpan.FromSeconds(2) }, retryDelays);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectUser, originalUsername);
                Environment.SetEnvironmentVariable(ContainerHelpers.PullHostObjectPass, originalPassword);
            }
        }

        /// <summary>
        /// Fails the first token GET or identity-token POST with a retryable transport error,
        /// then verifies that authentication recovers and caches the successfully accepted token.
        /// </summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task RetryableFailureDuringAuthenticationRecovers(bool useIdentityToken)
        {
            // Select either Basic token GET or refresh-token OAuth POST without inherited credential overrides.
            string registryName = $"auth-request-retry-test-{Guid.NewGuid():N}.invalid";
            string requestUrl = $"https://{registryName}/v2";
            string realmUrl = "https://auth.test/token";
            string identityToken = useIdentityToken ? "identity-token" : "";
            int authenticationRequestCount = 0;
            var retryDelays = new List<TimeSpan>();
            var registryAuthorizations = new List<string?>();
            using var authFile = new RegistryAuthFileScope(
                $$"""
                {
                    "auths": {
                        "{{registryName}}": {
                            "auth": "{{GetUserPasswordBase64("user", "pass")}}",
                            "identitytoken": "{{identityToken}}"
                        }
                    }
                }
                """);

            // Challenge unauthenticated requests; fail token acquisition once, then issue a usable token.
            using var httpClient = new HttpClient(
                new AuthHandshakeMessageHandler(
                    registryName,
                    new Uri(requestUrl),
                    isInsecureRegistry: false,
                    new ServerMessageHandler(request =>
                    {
                        registryAuthorizations.Add(request.Headers.Authorization?.Parameter);
                        return request.Headers.Authorization?.Parameter == "registry-token"
                            ? new HttpResponseMessage(HttpStatusCode.OK)
                            : CreateRequestAuthenticateResponse("Bearer", $"realm=\"{realmUrl}\"");
                    }),
                    NullLogger.Instance,
                    RegistryMode.Pull,
                    realmHandlerFactory: _ => new ServerMessageHandler(request =>
                    {
                        Assert.AreEqual(useIdentityToken ? HttpMethod.Post : HttpMethod.Get, request.Method);
                        if (++authenticationRequestCount == 1)
                        {
                            throw new HttpRequestException(
                                HttpRequestError.ConnectionError,
                                "Simulated connection failure.",
                                new IOException(
                                    "Simulated I/O failure.",
                                    new SocketException()));
                        }

                        return CreateBearerTokenResponse("registry-token");
                    }),
                    delay: CreateRecordingDelay(retryDelays)));

            // A network failure before obtaining a token must not consume the completed-handshake allowance.
            using HttpResponseMessage response = await httpClient.GetAsync(requestUrl, TestContext.CancellationToken);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            // A later request must reuse the recovered token rather than start another acquisition.
            using HttpResponseMessage cachedResponse = await httpClient.GetAsync(requestUrl, TestContext.CancellationToken);
            Assert.AreEqual(HttpStatusCode.OK, cachedResponse.StatusCode);
            Assert.AreEqual(2, authenticationRequestCount);
            Assert.AreSequenceEqual(new[] { TimeSpan.FromSeconds(2) }, retryDelays);
            Assert.AreSequenceEqual(
                new string?[] { null, null, "registry-token", "registry-token" },
                registryAuthorizations);
        }

        /// <summary>
        /// Keeps token acquisition failing with a retryable transport error and verifies that
        /// the existing five-attempt retry limit still terminates the request with all failures preserved.
        /// Records the backoff sequence without waiting for the requested delays.
        /// </summary>
        [TestMethod]
        public async Task RetryableFailureDuringAuthenticationStopsAtRetryLimit()
        {
            // Give the registry valid credentials so every attempt reaches the failing token transport.
            string registryName = $"auth-request-limit-test-{Guid.NewGuid():N}.invalid";
            string requestUrl = $"https://{registryName}/v2";
            string realmUrl = "https://auth.test/token";
            int registryRequestCount = 0;
            int authenticationRequestCount = 0;
            var retryDelays = new List<TimeSpan>();
            var expectedFailure = new HttpRequestException(
                HttpRequestError.ConnectionError,
                "Simulated connection failure.",
                new IOException("Simulated I/O failure.", new SocketException()));
            using var authFile = new RegistryAuthFileScope(
                $$"""
                {
                    "auths": {
                        "{{registryName}}": {
                            "auth": "{{GetUserPasswordBase64("user", "pass")}}"
                        }
                    }
                }
                """);

            // Return a challenge on every registry attempt and fail every token request before it yields authentication.
            using var httpClient = new HttpClient(
                new AuthHandshakeMessageHandler(
                    registryName,
                    new Uri(requestUrl),
                    isInsecureRegistry: false,
                    new ServerMessageHandler(request =>
                    {
                        registryRequestCount++;
                        Assert.IsNull(request.Headers.Authorization);
                        return CreateRequestAuthenticateResponse("Bearer", $"realm=\"{realmUrl}\"");
                    }),
                    NullLogger.Instance,
                    RegistryMode.Pull,
                    realmHandlerFactory: _ => new ServerMessageHandler(_ =>
                    {
                        authenticationRequestCount++;
                        throw expectedFailure;
                    }),
                    delay: CreateRecordingDelay(retryDelays)));

            // The bounded transport-retry policy must fail rather than return an unrelated 401 or loop indefinitely.
            ApplicationException exception = await Assert.ThrowsExactlyAsync<ApplicationException>(() =>
                httpClient.GetAsync(requestUrl, TestContext.CancellationToken));

            // Exactly five acquisition attempts exhaust the shared retry budget, preserving each original failure.
            Assert.AreEqual(5, registryRequestCount);
            Assert.AreEqual(5, authenticationRequestCount);
            Assert.AreSequenceEqual(
                new[] { 2, 4, 8, 16, 32 }.Select(seconds => TimeSpan.FromSeconds(seconds)),
                retryDelays);
            AggregateException failures = Assert.IsInstanceOfType<AggregateException>(exception.InnerException);
            Assert.HasCount(5, failures.InnerExceptions);
            foreach (Exception failure in failures.InnerExceptions)
            {
                Assert.AreSame(expectedFailure, failure);
            }
        }
    }
}
