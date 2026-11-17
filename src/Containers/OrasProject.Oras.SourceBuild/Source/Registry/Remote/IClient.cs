// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Registry/Remote/IClient.cs
// Copyright The ORAS Authors.
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Registry.Remote.Auth;

/// <summary>
/// IClient is an interface that provides abstraction for the method SendAsync.
/// </summary>
public interface IClient
{
    /// <summary>
    /// Sends an HTTP request asynchronously with optional auth cache partitioning and redirect
    /// control.
    /// </summary>
    /// <param name="originalRequest">The HTTP request message to send.</param>
    /// <param name="partitionId">
    /// Optional cache partition identifier. When provided, tokens are isolated by this ID,
    /// enabling multi-partition scenarios where different credentials are used for the same
    /// registry.
    /// </param>
    /// <param name="allowAutoRedirect">
    /// Whether to follow redirects automatically. When <c>false</c>, captures redirect locations
    /// without following them (e.g., for blob location retrieval).
    /// </param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the HTTP
    /// response message.
    /// </returns>
    Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage originalRequest,
        string? partitionId = null,
        bool allowAutoRedirect = true,
        CancellationToken cancellationToken = default);
}
