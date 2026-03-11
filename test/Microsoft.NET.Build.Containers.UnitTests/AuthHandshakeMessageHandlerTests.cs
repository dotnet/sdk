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

                var authHandler = new AuthHandshakeMessageHandler(TestRegistryName, new ServerMessageHandler(server), NullLogger.Instance, RegistryMode.Push);
                using var httpClient = new HttpClient(authHandler);

                var response = await httpClient.GetAsync(RequestUrl);
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
    }
}
