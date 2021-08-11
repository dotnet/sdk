﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.HotReload
{
    public class UpdatePayloadtest
    {
        [Fact]
        public async Task UpdatePayload_CanRoundTrip()
        {
            var initial = new UpdatePayload
            {
                Deltas = new[]
                {
                    new UpdateDelta
                    {
                        ModuleId = Guid.NewGuid(),
                        ILDelta = new byte[] { 0, 0, 1 },
                        MetadataDelta = new byte[] { 0, 1, 1 },
                    },
                    new UpdateDelta
                    {
                        ModuleId = Guid.NewGuid(),
                        ILDelta = new byte[] { 1, 0, 0 },
                        MetadataDelta = new byte[] { 1, 0, 1 },
                    }
                },
            };

            using var stream = new MemoryStream();
            await initial.WriteAsync(stream, default);

            stream.Position = 0;
            var read = await UpdatePayload.ReadAsync(stream, default);

            AssertEqual(initial, read);
        }

        [Fact]
        public async Task UpdatePayload_CanRoundTripUpdatedTypes()
        {
            var initial = new UpdatePayload
            {
                Deltas = new[]
                {
                    new UpdateDelta
                    {
                        ModuleId = Guid.NewGuid(),
                        ILDelta = new byte[] { 0, 0, 1 },
                        MetadataDelta = new byte[] { 0, 1, 1 },
                        UpdatedTypes = new int[] { 60, 74, 22323 },
                    },
                    new UpdateDelta
                    {
                        ModuleId = Guid.NewGuid(),
                        ILDelta = new byte[] { 1, 0, 0 },
                        MetadataDelta = new byte[] { 1, 0, 1 },
                        UpdatedTypes = new int[] { -18 },
                    }
                },
            };

            using var stream = new MemoryStream();
            await initial.WriteAsync(stream, default);

            stream.Position = 0;
            var read = await UpdatePayload.ReadAsync(stream, default);

            AssertEqual(initial, read);
        }

        [Fact]
        public async Task UpdatePayload_WithLargeDeltas_CanRoundtrip()
        {
            var initial = new UpdatePayload
            {
                Deltas = new[]
                {
                    new UpdateDelta
                    {
                        ModuleId = Guid.NewGuid(),
                        ILDelta = Enumerable.Range(0, 68200).Select(c => (byte)(c%2)).ToArray(),
                        MetadataDelta = new byte[] { 0, 1, 1 },
                    },
                },
            };

            using var stream = new MemoryStream();
            await initial.WriteAsync(stream, default);

            stream.Position = 0;
            var read = await UpdatePayload.ReadAsync(stream, default);

            AssertEqual(initial, read);
        }

        private static void AssertEqual(UpdatePayload initial, UpdatePayload read)
        {
            Assert.Equal(initial.Deltas.Count, read.Deltas.Count);

            for (var i = 0; i < initial.Deltas.Count; i++)
            {
                var e = initial.Deltas[i];
                var a = read.Deltas[i];

                Assert.Equal(e.ModuleId, a.ModuleId);
                Assert.Equal(e.ILDelta, a.ILDelta);
                Assert.Equal(e.MetadataDelta, a.MetadataDelta);
                if (e.UpdatedTypes is null)
                {
                    Assert.Empty(a.UpdatedTypes);
                }
                else
                {
                    Assert.Equal(e.UpdatedTypes, a.UpdatedTypes);
                }
            }
        }
    }
}
