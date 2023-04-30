// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Microsoft.NET.Sdk.Publish.Tasks.ZipDeploy
{
    /// <summary>
    /// A response to an HTTP request
    /// </summary>
    public interface IHttpResponse
    {
        /// <summary>
        /// Gets the status code the server returned
        /// </summary>
        HttpStatusCode StatusCode { get; }

        /// <summary>
        /// Gets the body of the response
        /// </summary>
        Task<Stream> GetResponseBodyAsync();

        IEnumerable<string> GetHeader(string name);
    }
}
