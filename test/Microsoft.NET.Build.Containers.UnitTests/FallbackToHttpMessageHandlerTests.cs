// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.NET.Build.Containers.UnitTests
{
    public class FallbackToHttpMessageHandlerTests
    {
        [Theory]
        [InlineData("mcr.microsoft.com", 80)]
        [InlineData("mcr.microsoft.com:443", 443)]
        [InlineData("mcr.microsoft.com:80", 80)]
        [InlineData("mcr.microsoft.com:5555", 5555)]
        [InlineData("[2408:8120:245:49a0:f041:d7bb:bb13:5b64]", 80)]
        [InlineData("[2408:8120:245:49a0:f041:d7bb:bb13:5b64]:443", 443)]
        [InlineData("[2408:8120:245:49a0:f041:d7bb:bb13:5b64]:80", 80)]
        [InlineData("[2408:8120:245:49a0:f041:d7bb:bb13:5b64]:5555", 5555)]
        public async Task FallBackToHttpPortShouldAsExpected(string registry, int expectedPort)
        {
            var uri = new Uri($"https://{registry}");
            var handler = new FallbackToHttpMessageHandler(
                registry,
                uri.Host,
                uri.Port,
                new ServerMessageHandler(request =>
                {
                    // only accept http requests, reject https requests with a secure connection error

                    if (request.RequestUri!.Scheme == Uri.UriSchemeHttps)
                    {
                        throw new HttpRequestException(
                            httpRequestError: HttpRequestError.SecureConnectionError
                        );
                    }
                    else
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            RequestMessage = request,
                        };
                    }
                }),
                NullLogger.Instance
            );
            using var httpClient = new HttpClient(handler);
            var response = await httpClient.GetAsync(uri);
            Assert.Equal(expectedPort, response.RequestMessage?.RequestUri?.Port);
        }

        private sealed class ServerMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _server;

            public ServerMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> server)
            {
                _server = server;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken
            )
            {
                return Task.FromResult(_server(request));
            }
        }
    }
}
