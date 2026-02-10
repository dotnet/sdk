// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;

namespace Microsoft.NET.Sdk.Publish.Tasks;

internal class HttpResponseMessageForStatusCode(HttpStatusCode statusCode) : IHttpResponse
{
    /// <inheritdoc/>
    public HttpStatusCode StatusCode { get; private set; } = statusCode;

    /// <inheritdoc/>
    public Task<Stream> GetResponseBodyAsync()
    {
        return System.Threading.Tasks.Task.FromResult<Stream>(new MemoryStream());
    }

    /// <inheritdoc/>
    public IEnumerable<string> GetHeader(string name)
    {
        return [];
    }
}
