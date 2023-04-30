// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Microsoft.NET.Sdk.Publish.Tasks.ZipDeploy
{
    internal class HttpResponseMessageForStatusCode : IHttpResponse
    {
        public HttpResponseMessageForStatusCode(HttpStatusCode statusCode)
        {
            StatusCode = statusCode;
        }

        public HttpStatusCode StatusCode { get; private set; }

        public Task<Stream> GetResponseBodyAsync()
        {
            return Task.FromResult<Stream>(new MemoryStream());
        }

        public IEnumerable<string> GetHeader(string name)
        {
            return new string[0];
        }
    }
}
