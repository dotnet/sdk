// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.Watch.BrowserRefresh
{
    public class BlazorWasmHotReloadMiddlewareTest
    {
        [Fact]
        public async Task DeltasAreSavedOnPost()
        {
            var middleware = new BlazorWasmHotReloadMiddleware(context => throw new TimeZoneNotFoundException());

            var context = new DefaultHttpContext();
            context.Request.Method = "post";
            var update = new BlazorWasmHotReloadMiddleware.Update
            {
                Id = 0,
                Deltas =
                [
                    new()
                    {
                        ModuleId = Guid.NewGuid().ToString(),
                        ILDelta = "ILDelta1",
                        PdbDelta = "PDBDelta1",
                        MetadataDelta = "MetadataDelta1",
                        UpdatedTypes = [42],
                    },
                    new()
                    {
                        ModuleId = Guid.NewGuid().ToString(),
                        ILDelta = "ILDelta2",
                        PdbDelta = "PDBDelta2",
                        MetadataDelta = "MetadataDelta2",
                        UpdatedTypes = [42],
                    }
                ]
            };

            context.Request.Body = GetJson(update);

            await middleware.InvokeAsync(context);

            AssertUpdates([update], middleware.Updates);
        }

        [Fact]
        public async Task DuplicateDeltasOnPostAreIgnored()
        {
            var middleware = new BlazorWasmHotReloadMiddleware(context => throw new TimeZoneNotFoundException());

            var updates = new BlazorWasmHotReloadMiddleware.Update[]
            {
                new()
                {
                    Id = 0,
                    Deltas =
                    [
                        new()
                        {
                            ModuleId = Guid.NewGuid().ToString(),
                            ILDelta = "ILDelta1",
                            PdbDelta = "PDBDelta1",
                            MetadataDelta = "MetadataDelta1",
                            UpdatedTypes = [42],
                        }
                    ]
                },
                new()
                {
                    Id = 1,
                    Deltas =
                    [
                        new()
                        {
                            ModuleId = Guid.NewGuid().ToString(),
                            ILDelta = "ILDelta2",
                            PdbDelta = "PDBDelta2",
                            MetadataDelta = "MetadataDelta2",
                            UpdatedTypes = [42],
                        }
                    ]
                }
            };

            var context = new DefaultHttpContext();
            context.Request.Method = "post";
            context.Request.Body = GetJson(updates[0]);

            await middleware.InvokeAsync(context);

            context = new DefaultHttpContext();
            context.Request.Method = "post";
            context.Request.Body = GetJson(updates[1]);
            await middleware.InvokeAsync(context);

            // Assert
            AssertUpdates(updates, middleware.Updates);
        }

        [Fact]
        public async Task MultipleDeltaPayloadsCanBeAccepted()
        {
            var middleware = new BlazorWasmHotReloadMiddleware(context => throw new TimeZoneNotFoundException());

            var update = new BlazorWasmHotReloadMiddleware.Update()
            {
                Id = 0,
                Deltas =
                [
                    new()
                    {
                        ModuleId = Guid.NewGuid().ToString(),
                        ILDelta = "ILDelta1",
                        PdbDelta = "PDBDelta1",
                        MetadataDelta = "MetadataDelta1",
                        UpdatedTypes = [42],
                    },
                    new()
                    {
                        ModuleId = Guid.NewGuid().ToString(),
                        ILDelta = "ILDelta2",
                        PdbDelta = "PDBDelta2",
                        MetadataDelta = "MetadataDelta2",
                        UpdatedTypes = [42],
                    }
                ]
            };

            var context = new DefaultHttpContext();
            context.Request.Method = "post";
            context.Request.Body = GetJson(update);
            await middleware.InvokeAsync(context);

            var newUpdate = new BlazorWasmHotReloadMiddleware.Update()
            {
                Id = 1,
                Deltas =
                [
                    new()
                    {
                        ModuleId = Guid.NewGuid().ToString(),
                        ILDelta = "ILDelta3",
                        PdbDelta = "PDBDelta3",
                        MetadataDelta = "MetadataDelta3",
                        UpdatedTypes = [42],
                    },
                    new()
                    {
                        ModuleId = Guid.NewGuid().ToString(),
                        ILDelta = "ILDelta4",
                        PdbDelta = "PDBDelta4",
                        MetadataDelta = "MetadataDelta4",
                        UpdatedTypes = [42],
                    },
                    new()
                    {
                        ModuleId = Guid.NewGuid().ToString(),
                        ILDelta = "ILDelta5",
                        PdbDelta = "PDBDelta5",
                        MetadataDelta = "MetadataDelta5",
                        UpdatedTypes = [42],
                    },
                ]
            };

            context = new DefaultHttpContext();
            context.Request.Method = "post";
            context.Request.Body = GetJson(newUpdate);
            await middleware.InvokeAsync(context);

            AssertUpdates([update, newUpdate], middleware.Updates);
        }

        [Fact]
        public async Task Get_Returns204_IfNoDeltasPresent()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Method = "get";
            var middleware = new BlazorWasmHotReloadMiddleware(context => throw new TimeZoneNotFoundException());

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal(204, context.Response.StatusCode);
        }

        [Fact]
        public async Task GetReturnsDeltas()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Method = "get";
            var stream = new MemoryStream();
            context.Response.Body = stream;
            var middleware = new BlazorWasmHotReloadMiddleware(context => throw new TimeZoneNotFoundException());
            var updates = new List<BlazorWasmHotReloadMiddleware.Update>
            {
                new()
                {
                    Id = 0,
                    Deltas =
                    [
                        new()
                        {
                            ModuleId = Guid.NewGuid().ToString(),
                            ILDelta = "ILDelta1",
                            PdbDelta = "PdbDelta1",
                            MetadataDelta = "MetadataDelta1",
                            UpdatedTypes = [42],
                        },
                        new()
                        {
                            ModuleId = Guid.NewGuid().ToString(),
                            ILDelta = "ILDelta2",
                            PdbDelta = "PdbDelta2",
                            MetadataDelta = "MetadataDelta2",
                            UpdatedTypes = [42],
                        }
                    ]
                }
            };
            middleware.Updates.AddRange(updates);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal(200, context.Response.StatusCode);
            Assert.Equal(
                JsonSerializer.SerializeToUtf8Bytes(updates, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                stream.ToArray());
        }

        private static void AssertUpdates(IReadOnlyList<BlazorWasmHotReloadMiddleware.Update> expected, IReadOnlyList<BlazorWasmHotReloadMiddleware.Update> actual)
        {
            Assert.Equal(expected.Count, actual.Count);

            for (var u = 0; u < expected.Count; u++)
            {
                var expectedUpdate = expected[u];
                var actualUpdate = actual[u];
                Assert.Equal(expectedUpdate.Id, actualUpdate.Id);
                Assert.Equal(expectedUpdate.Deltas.Length, expectedUpdate.Deltas.Length);

                for (var i = 0; i < expectedUpdate.Deltas.Length; i++)
                {
                    Assert.Equal(expectedUpdate.Deltas[i].ILDelta, actualUpdate.Deltas[i].ILDelta);
                    Assert.Equal(expectedUpdate.Deltas[i].PdbDelta, actualUpdate.Deltas[i].PdbDelta);
                    Assert.Equal(expectedUpdate.Deltas[i].MetadataDelta, actualUpdate.Deltas[i].MetadataDelta);
                    Assert.Equal(expectedUpdate.Deltas[i].ModuleId, actualUpdate.Deltas[i].ModuleId);
                    Assert.Equal(expectedUpdate.Deltas[i].UpdatedTypes, actualUpdate.Deltas[i].UpdatedTypes);
                }
            }
        }

        private static Stream GetJson(object obj)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(obj, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return new MemoryStream(bytes);
        }
    }
}
