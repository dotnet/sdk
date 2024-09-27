// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Http;

namespace Microsoft.NET.Sdk.Publish.Tasks;

internal class HttpResponseMessageWrapper : IHttpResponse
{
    private readonly HttpResponseMessage _message;
    private readonly Lazy<Task<Stream>> _responseBodyTask;

    public HttpResponseMessageWrapper(HttpResponseMessage message)
    {
        _message = message;
        _responseBodyTask = new Lazy<Task<Stream>>(GetResponseStream);

        StatusCode = message?.StatusCode ?? HttpStatusCode.InternalServerError;
    }

    /// <inheritdoc/>
    public HttpStatusCode StatusCode { get; private set; }

    /// <inheritdoc/>
    public async Task<Stream> GetResponseBodyAsync()
    {
        return _responseBodyTask.Value is not null
            ? await _responseBodyTask.Value
            : null;
    }

    /// <inheritdoc/>
    public IEnumerable<string> GetHeader(string name)
    {
        if (_message is not null
            && _message.Headers is not null
            && _message.Headers.TryGetValues(name, out IEnumerable<string> values))
        {
            return values;
        }

        return [];
    }

    private Task<Stream> GetResponseStream()
    {
        return _message is not null && _message.Content is not null
            ? _message.Content.ReadAsStreamAsync()
            : null;
    }
}
