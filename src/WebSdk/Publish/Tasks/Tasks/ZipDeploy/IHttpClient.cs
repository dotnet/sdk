// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.NET.Sdk.Publish.Tasks.ZipDeploy
{
    public interface IHttpClient
    {
        HttpRequestHeaders DefaultRequestHeaders { get; }
        Task<HttpResponseMessage> PostAsync(Uri uri, StreamContent content);
        Task<HttpResponseMessage> GetAsync(Uri uri, CancellationToken cancellationToken);
    }
}
