// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Registry/Remote/Registry.cs
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

using OrasProject.Oras.Exceptions;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using OrasProject.Oras.Registry.Remote.Auth;
using OrasProject.Oras.Serialization;

namespace OrasProject.Oras.Registry.Remote;

/// <summary>
/// Registry provides access to a remote registry that implements the Docker Registry
/// HTTP API V2 or the OCI Distribution Specification.
/// </summary>
public class Registry : IRegistry
{
    /// <summary>
    /// Gets the options used to access repositories in this registry.
    /// </summary>
    public RepositoryOptions RepositoryOptions => _opts;

    private RepositoryOptions _opts;

    /// <summary>
    /// Initializes a new instance of the <see cref="Registry"/> class for the given
    /// registry host, using the default HTTP client.
    /// </summary>
    /// <param name="registry">The registry host, e.g. <c>registry.example.com</c>.</param>
    public Registry(string registry) : this(registry, new PlainClient()) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Registry"/> class for the given
    /// registry host, using the specified HTTP client.
    /// </summary>
    /// <param name="registry">The registry host, e.g. <c>registry.example.com</c>.</param>
    /// <param name="client">The client used to send HTTP requests.</param>
    public Registry(string registry, IClient client) => _opts = new()
    {
        Reference = new Reference(registry),
        Client = client,
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="Registry"/> class from the given options.
    /// </summary>
    /// <param name="options">The repository options used to access the registry.</param>
    public Registry(RepositoryOptions options) => _opts = options;

    /// <summary>
    /// PingAsync checks whether or not the registry implement Docker Registry API V2 or
    ///  OCI Distribution Specification.
    ///  Ping can be used to check authentication when an auth client is configured.
    ///  References:
    ///   - https://docs.docker.com/registry/spec/api/#base
    ///   - https://github.com/opencontainers/distribution-spec/blob/v1.0.1/spec.md#api
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task PingAsync(CancellationToken cancellationToken = default)
    {
        var url = new UriFactory(_opts).BuildRegistryBase();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var resp = await _opts.Client.SendAsync(
            request,
            partitionId: _opts.PartitionId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        switch (resp.StatusCode)
        {
            case HttpStatusCode.OK:
                return;
            case HttpStatusCode.NotFound:
                throw new NotFoundException($"Repository {_opts.Reference} not found");
            default:
                throw await resp.ParseErrorResponseAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Repository returns a repository object for the given repository name.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<IRepository> GetRepositoryAsync(string name, CancellationToken cancellationToken = default)
    {
        var reference = new Reference(_opts.Reference.Registry, name);
        var options = _opts; // shallow copy
        options.Reference = reference;
        return Task.FromResult<IRepository>(new Repository(options));
    }

    /// <summary>
    /// Repositories returns a list of repositories from the remote registry.
    /// </summary>
    /// <param name="last"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async IAsyncEnumerable<string> ListRepositoriesAsync(string? last = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (Scope.TryParse(Scope.ScopeRegistryCatalog, out var scope))
        {
            ScopeManager.SetScopeForRegistry(
                RepositoryOptions.Client,
                RepositoryOptions.Reference.Registry,
                scope,
                RepositoryOptions.PartitionId);
        }

        var url = new UriFactory(_opts).BuildRegistryCatalog();
        do
        {
            (var repositories, url) = await FetchRepositoryPageAsync(last, url!, cancellationToken).ConfigureAwait(false);
            last = null;
            foreach (var repository in repositories)
            {
                yield return repository;
            }
        } while (url != null);
    }

    /// <summary>
    /// FetchRepositoryPageAsync returns a single page of repositories list with the next link
    /// </summary>
    /// <param name="last"></param>
    /// <param name="url"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task<(string[], Uri?)> FetchRepositoryPageAsync(string? last, Uri url, CancellationToken cancellationToken = default)
    {
        var uriBuilder = new UriBuilder(url);
        if (_opts.TagListPageSize > 0 || !string.IsNullOrEmpty(last))
        {
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            if (_opts.TagListPageSize > 0)
            {
                query["n"] = _opts.TagListPageSize.ToString();
            }
            if (!string.IsNullOrEmpty(last))
            {
                query["last"] = last;
            }
            uriBuilder.Query = query.ToString();
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, uriBuilder.ToString());
        using var response = await _opts.Client.SendAsync(
            request,
            partitionId: _opts.PartitionId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw await response.ParseErrorResponseAsync(cancellationToken).ConfigureAwait(false);
        }
        var data = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var repositories = OciJsonSerializer.Deserialize<RepositoryList>(data);
        return (repositories.Repositories ?? Array.Empty<string>(), response.ParseLink());
    }

    internal struct RepositoryList
    {
        [JsonPropertyName("repositories")]
        public string[]? Repositories { get; set; }
    }
}
