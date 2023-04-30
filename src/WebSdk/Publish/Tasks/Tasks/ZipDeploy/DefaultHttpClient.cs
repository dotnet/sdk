// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.NET.Sdk.Publish.Tasks.ZipDeploy
{
    internal class DefaultHttpClient : IHttpClient, IDisposable
    {
        private readonly HttpClient _httpClient = new HttpClient()
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        public HttpRequestHeaders DefaultRequestHeaders => _httpClient.DefaultRequestHeaders;

        public void Dispose()
        {
            _httpClient.Dispose();
        }

        public Task<HttpResponseMessage> PostAsync(Uri uri, StreamContent content)
        {
            return _httpClient.PostAsync(uri, content);
        }

        public Task<HttpResponseMessage> GetAsync(Uri uri, CancellationToken cancellationToken)
        {
            return _httpClient.GetAsync(uri, cancellationToken);
        }
    }
}
