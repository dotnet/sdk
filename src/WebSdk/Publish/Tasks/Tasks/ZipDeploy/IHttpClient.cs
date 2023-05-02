// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
