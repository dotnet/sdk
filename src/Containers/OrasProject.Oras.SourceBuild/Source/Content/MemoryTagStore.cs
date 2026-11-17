// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Content/MemoryTagStore.cs
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
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Content;

internal class MemoryTagStore : ITagStore
{
    private readonly ConcurrentDictionary<string, Descriptor> _index = new();

    public Task<Descriptor> ResolveAsync(string reference, CancellationToken _ = default)
    {
        if (!_index.TryGetValue(reference, out var content))
        {
            throw new NotFoundException();
        }
        return Task.FromResult(content);
    }

    public Task TagAsync(Descriptor descriptor, string reference, CancellationToken _ = default)
    {
        _index.AddOrUpdate(reference, descriptor, (_, _) => descriptor);
        return Task.CompletedTask;
    }
}
