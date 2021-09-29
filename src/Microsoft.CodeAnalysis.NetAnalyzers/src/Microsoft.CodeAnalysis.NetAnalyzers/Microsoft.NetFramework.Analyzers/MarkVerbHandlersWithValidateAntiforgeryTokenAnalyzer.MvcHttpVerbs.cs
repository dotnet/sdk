// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;

namespace Microsoft.NetFramework.Analyzers
{
    public partial class MarkVerbHandlersWithValidateAntiforgeryTokenAnalyzer
    {
        /// <summary>
        /// ASP.NET MVC's implementation of HttpVerbs.
        /// </summary>
        [Flags]
        private enum MvcHttpVerbs
        {
            None = 0,

            /// <summary>
            /// Retrieves the information or entity that is identified by the URI of the request.
            /// </summary>
            Get = 1,

            /// <summary>
            /// Posts a new entity as an addition to a URI.
            /// </summary>
            Post = 2,

            /// <summary>
            /// Replaces an entity that is identified by a URI.
            /// </summary>
            Put = 4,

            /// <summary>
            /// Requests that a specified URI be deleted.
            /// </summary>
            Delete = 8,

            /// <summary>
            /// Retrieves the message headers for the information or entity that is identified by the URI of the request.
            /// </summary>
            Head = 0x10,

            /// <summary>
            /// Requests that a set of changes described in the request entity be applied to the resource identified by the Request-URI.
            /// </summary>
            Patch = 0x20,

            /// <summary>
            /// Represents a request for information about the communication options available on the request/response chain identified by the Request-URI.
            /// </summary>
            Options = 0x40
        }
    }
}
