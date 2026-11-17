// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Content/LimitedStorage.cs
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

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OrasProject.Oras.Exceptions;
using OrasProject.Oras.Oci;

namespace OrasProject.Oras.Content;

/// <summary>
/// Provides a storage wrapper that enforces a maximum size limit for pushed content.
/// </summary>
/// <remarks>
/// This class wraps an <see cref="IStorage"/> instance and restricts the size of content that can be pushed to it.
/// If the content size exceeds the specified limit, a <see cref="SizeLimitExceededException"/> is thrown.
/// </remarks>
/// <param name="storage">The underlying storage to wrap.</param>
/// <param name="limit">The maximum allowed size (in bytes) for pushed content.</param>
internal class LimitedStorage(IStorage storage, long limit) : IStorage
{
    private readonly IStorage _storage = storage;
    private readonly long _pushLimit = limit;

    public Task<bool> ExistsAsync(Descriptor target, CancellationToken cancellationToken = default)
    {
        return _storage.ExistsAsync(target, cancellationToken);
    }

    public Task<Stream> FetchAsync(Descriptor target, CancellationToken cancellationToken = default)
    {
        return _storage.FetchAsync(target, cancellationToken);
    }

    public async Task PushAsync(Descriptor expected, Stream stream, CancellationToken cancellationToken = default)
    {
        if (expected.Size > _pushLimit)
        {
            throw new SizeLimitExceededException($"content size {expected.Size} exceeds push size limit {_pushLimit}");
        }
        await _storage.PushAsync(expected, new LimitedReadStream(stream, _pushLimit), cancellationToken).ConfigureAwait(false);
    }
}
