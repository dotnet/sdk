// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Registry/ITagListable.cs
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

namespace OrasProject.Oras.Registry;

/// <summary>
/// Lists tags by the tag service.
/// </summary>
public interface ITagListable
{
    /// <summary>
    /// Lists the tags available in the repository.
    /// Since the returned tag list may be paginated by the underlying
    /// implementation, a function should be passed in to process the paginated
    /// tag list.
    /// Note: When implemented by a remote registry, the tags API is called.
    /// However, not all registries supports pagination or conforms the
    /// specification.
    /// References:
    /// - https://github.com/opencontainers/distribution-spec/blob/v1.0.1/spec.md
    /// - https://docs.docker.com/registry/spec/api/#tags
    /// </summary>
    /// <param name="last"> The `last` parameter when invoking the tags API. If `last` is NOT empty, the entries in the response start after the tag specified by `last`. Otherwise, the response starts from the top of the Tags list.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    IAsyncEnumerable<string> ListTagsAsync(string? last = default, CancellationToken cancellationToken = default);
}
