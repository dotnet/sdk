// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Content/MemoryStorage.cs
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
using OrasProject.Oras.Oci;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Content;

internal class MemoryStorage : IStorage
{
    private readonly ConcurrentDictionary<BasicDescriptor, byte[]> _content = new();

    public Task<bool> ExistsAsync(Descriptor target, CancellationToken _ = default)
    {
        return Task.FromResult(_content.ContainsKey(target.BasicDescriptor));
    }

    public Task<Stream> FetchAsync(Descriptor target, CancellationToken _ = default)
    {
        if (!_content.TryGetValue(target.BasicDescriptor, out var content))
        {
            throw new NotFoundException($"{target.Digest}: {target.MediaType}");
        }
        return Task.FromResult<Stream>(new MemoryStream(content));
    }

    public async Task PushAsync(Descriptor expected, Stream contentStream, CancellationToken cancellationToken = default)
    {
        var key = expected.BasicDescriptor;
        if (_content.ContainsKey(key))
        {
            throw new AlreadyExistsException($"{expected.Digest}: {expected.MediaType}");
        }

        var content = await contentStream.ReadAllAsync(expected, cancellationToken).ConfigureAwait(false);
        if (!_content.TryAdd(key, content))
        {
            throw new AlreadyExistsException($"{key.Digest}: {key.MediaType}");
        }
    }
}
