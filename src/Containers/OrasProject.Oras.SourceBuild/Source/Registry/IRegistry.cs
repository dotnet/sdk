// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Registry/IRegistry.cs
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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Registry;

/// <summary>
/// IRegistry represents a remote registry that provides access to its repositories.
/// </summary>
public interface IRegistry
{
    /// <summary>
    /// Returns a repository reference by the given name.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IRepository> GetRepositoryAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Repositories lists the name of repositories available in the registry.
    /// Since the returned repositories may be paginated by the underlying
    /// implementation, a function should be passed in to process the paginated
    /// repository list.
    /// `last` argument is the `last` parameter when invoking the catalog API.
    /// If `last` is NOT empty, the entries in the response start after the
    /// repo specified by `last`. Otherwise, the response starts from the top
    /// of the Repositories list.
    /// Note: When implemented by a remote registry, the catalog API is called.
    /// However, not all registries supports pagination or conforms the
    /// specification.
    /// Reference: https://docs.docker.com/registry/spec/api/#catalog
    /// See also `Repositories()` in this package.
    /// </summary>
    /// <param name="last"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    IAsyncEnumerable<string> ListRepositoriesAsync(string? last = default, CancellationToken cancellationToken = default);
}
