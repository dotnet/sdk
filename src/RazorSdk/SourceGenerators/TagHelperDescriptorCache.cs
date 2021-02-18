// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    // This type is a temporary workaround until the compiler supports granular caching. See https://github.com/dotnet/roslyn/issues/51257
    internal static class TagHelperDescriptorCache
    {
        private static readonly ConcurrentDictionary<CacheKey, IReadOnlyList<TagHelperDescriptor>> _tagHelperDescriptorCache = new();

        public static IReadOnlyList<TagHelperDescriptor> GetOrAdd<TState>(IEnumerable<MetadataReference> references, Func<TState, IReadOnlyList<TagHelperDescriptor>> create, TState state)
        {
            var cacheKey = CacheKey.Create(references);
            if (!_tagHelperDescriptorCache.TryGetValue(cacheKey, out var descriptors))
            {
                descriptors = create(state);

                if (_tagHelperDescriptorCache.Count > 500)
                {
                    _tagHelperDescriptorCache.Clear();
                }

                _tagHelperDescriptorCache[cacheKey] = descriptors;
            }

            return descriptors;
        }

        private readonly struct CacheKey : IEquatable<CacheKey>
        {
            private readonly ImmutableArray<MetadataId> _mvids;

            public CacheKey(ImmutableArray<MetadataId> mvids)
            {
                _mvids = mvids;
            }

            public static CacheKey Create(IEnumerable<MetadataReference> references)
            {
                var mvids = ImmutableArray.CreateBuilder<MetadataId>();

                foreach (var reference in references)
                {
                    if (reference is not PortableExecutableReference peReference)
                    {
                        return default;
                    }

                    mvids.Add(peReference.GetMetadataId());
                }

                return new CacheKey(mvids.ToImmutable());
            }

            public bool Equals(CacheKey other)
            {
                if (_mvids.Length != other._mvids.Length)
                {
                    return false;
                }

                for (var i = 0; i < _mvids.Length; i++)
                {
                    if (!object.ReferenceEquals(_mvids[i], other._mvids[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            public override int GetHashCode() => _mvids.Length;
        }

    }
}
