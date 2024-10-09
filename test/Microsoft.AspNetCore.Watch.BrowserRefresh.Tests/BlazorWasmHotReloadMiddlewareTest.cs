// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
#if F
namespace Microsoft.AspNetCore.Watch.BrowserRefresh
{
    public class BlazorWasmHotReloadMiddlewareTest
    {
        [Fact]
        public async Task DeltasAreSavedOnPost()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Method = "post";
            var updates = new BlazorWasmHotReloadMiddleware.Update[]
            {
                new()
                {
                    SequenceId = 0,
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
                }
            };
            context.Request.Body = GetJson(updates);

            var middleware = new BlazorWasmHotReloadMiddleware(context => throw new TimeZoneNotFoundException());

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            AssertUpdates(updates, middleware.Updates);
        }

        [Fact]
        public async Task DuplicateDeltasOnPostAreIgnored()
        {
            // Arrange
            var updates = new BlazorWasmHotReloadMiddleware.Update[]
            {
                new()
                {
                    SequenceId = 0,
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
                    SequenceId = 1,
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
            context.Request.Body = GetJson(updates);

            var middleware = new BlazorWasmHotReloadMiddleware(context => throw new TimeZoneNotFoundException());

            // Act 1
            await middleware.InvokeAsync(context);

            // Act 2
            context = new DefaultHttpContext();
            context.Request.Method = "post";
            context.Request.Body = GetJson(updates);
            await middleware.InvokeAsync(context);

            // Assert
            AssertUpdates(updates, middleware.Updates);
        }

        [Fact]
        public async Task MultipleDeltaPayloadsCanBeAccepted()
        {
            // Arrange
            var updates = new BlazorWasmHotReloadMiddleware.Update[]
            {
                new()
                {
                    SequenceId = 0,
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
                    SequenceId = 1,
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
            context.Request.Body = GetJson(updates);

            var middleware = new BlazorWasmHotReloadMiddleware(context => throw new TimeZoneNotFoundException());

            // Act 1
            await middleware.InvokeAsync(context);

            // Act 2
            var newUpdate = new  new[]
            {
                new BlazorWasmHotReloadMiddleware.Update
                {
                    SequenceId = 3,
                    ModuleId = Guid.NewGuid().ToString(),
                    ILDelta = "ILDelta3",
                    MetadataDelta = "MetadataDelta3",
                    UpdatedTypes = [42],
                },
                new BlazorWasmHotReloadMiddleware.Update
                {
                    SequenceId = 4,
                    ModuleId = Guid.NewGuid().ToString(),
                    ILDelta = "ILDelta4",
                    MetadataDelta = "MetadataDelta4",
                    UpdatedTypes = [42],
                },
                    new BlazorWasmHotReloadMiddleware.Update
                {
                    SequenceId = 5,
                    ModuleId = Guid.NewGuid().ToString(),
                    ILDelta = "ILDelta5",
                    MetadataDelta = "MetadataDelta5",
                    UpdatedTypes = [42],
                },
            };

            context = new DefaultHttpContext();
            context.Request.Method = "post";
            context.Request.Body = GetJson(newUpdate);
            await middleware.InvokeAsync(context);

            // Assert
            deltas.AddRange(newUpdate);
            AssertUpdates(deltas, middleware.Updates);
            Assert.NotEqual(0, context.Response.Headers["ETag"].Count);
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
            var deltas = new List<BlazorWasmHotReloadMiddleware.Update>
            {
                new BlazorWasmHotReloadMiddleware.Update
                {
                    SequenceId = 0,
                    ModuleId = Guid.NewGuid().ToString(),
                    ILDelta = "ILDelta1",
                    MetadataDelta = "MetadataDelta1",
                    UpdatedTypes = [42],
                },
                new BlazorWasmHotReloadMiddleware.Update
                {
                    SequenceId = 1,
                    ModuleId = Guid.NewGuid().ToString(),
                    ILDelta = "ILDelta2",
                    MetadataDelta = "MetadataDelta2",
                    UpdatedTypes = [42],
                }
            };
            middleware.Updates.AddRange(deltas);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal(200, context.Response.StatusCode);
            Assert.Equal(
                JsonSerializer.SerializeToUtf8Bytes(deltas, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                stream.ToArray());
            Assert.NotEqual(0, context.Response.Headers[HeaderNames.ETag].Count);
        }

        [Fact]
        public async Task GetReturnsNotModified_IfNoneMatchApplies()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Method = "get";
            var middleware = new BlazorWasmHotReloadMiddleware(context => throw new TimeZoneNotFoundException());
            var deltas = new List<BlazorWasmHotReloadMiddleware.Update>
            {
                new BlazorWasmHotReloadMiddleware.Update
                {
                    SequenceId = 0,
                    ModuleId = Guid.NewGuid().ToString(),
                    ILDelta = "ILDelta1",
                    MetadataDelta = "MetadataDelta1",
                    UpdatedTypes = [42],
                },
                new BlazorWasmHotReloadMiddleware.Update
                {
                    SequenceId = 1,
                    ModuleId = Guid.NewGuid().ToString(),
                    ILDelta = "ILDelta2",
                    MetadataDelta = "MetadataDelta2",
                    UpdatedTypes = [42],
                }
            };
            middleware.Updates.AddRange(deltas);

            // Act 1
            await middleware.InvokeAsync(context);
            var etag = context.Response.Headers[HeaderNames.ETag];

            // Act 2
            context = new DefaultHttpContext();
            context.Request.Method = "get";
            context.Request.Headers[HeaderNames.IfNoneMatch] = etag;

            await middleware.InvokeAsync(context);

            // Assert 2
            Assert.Equal(StatusCodes.Status304NotModified, context.Response.StatusCode);
        }

        [Fact]
        public async Task GetReturnsUpdatedResults_IfNoneMatchFails()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Method = "get";
            var middleware = new BlazorWasmHotReloadMiddleware(context => throw new TimeZoneNotFoundException());
            var deltas = new List<BlazorWasmHotReloadMiddleware.Update>
            {
                new BlazorWasmHotReloadMiddleware.Update
                {
                    SequenceId = 0,
                    ModuleId = Guid.NewGuid().ToString(),
                    ILDelta = "ILDelta1",
                    MetadataDelta = "MetadataDelta1",
                    UpdatedTypes = [42],
                },
                new BlazorWasmHotReloadMiddleware.Update
                {
                    SequenceId = 1,
                    ModuleId = Guid.NewGuid().ToString(),
                    ILDelta = "ILDelta2",
                    MetadataDelta = "MetadataDelta2",
                    UpdatedTypes = [42],
                }
            };
            middleware.Updates.AddRange(deltas);

            // Act 1
            await middleware.InvokeAsync(context);
            var etag = context.Response.Headers[HeaderNames.ETag];

            // Act 2
            var update = new BlazorWasmHotReloadMiddleware.Update
            {
                SequenceId = 3,
                ModuleId = Guid.NewGuid().ToString(),
                ILDelta = "ILDelta3",
                MetadataDelta = "MetadataDelta3",
                UpdatedTypes = [42],
            };
            deltas.Add(update);
            middleware.Updates.Add(update);
            context = new DefaultHttpContext();
            context.Request.Method = "get";
            context.Request.Headers[HeaderNames.IfNoneMatch] = etag;
            var stream = new MemoryStream();
            context.Response.Body = stream;

            await middleware.InvokeAsync(context);

            // Assert 2
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
            Assert.Equal(
                JsonSerializer.SerializeToUtf8Bytes(deltas, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                stream.ToArray());
            Assert.NotEqual(etag, context.Response.Headers[HeaderNames.ETag]);
        }

        private static void AssertUpdates(IReadOnlyList<BlazorWasmHotReloadMiddleware.Update> expected, IReadOnlyList<BlazorWasmHotReloadMiddleware.Update> actual)
        {
            Assert.Equal(expected.Count, actual.Count);

            for (var i = 0; i < expected.Count; i++)
            {
                Assert.Equal(expected[i].ILDelta, actual[i].ILDelta);
                Assert.Equal(expected[i].MetadataDelta, actual[i].MetadataDelta);
                Assert.Equal(expected[i].ModuleId, actual[i].ModuleId);
                Assert.Equal(expected[i].SequenceId, actual[i].SequenceId);
                Assert.Equal(expected[i].UpdatedTypes, actual[i].UpdatedTypes);
            }
        }

        private Stream GetJson(IReadOnlyList<BlazorWasmHotReloadMiddleware.Update> deltas)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(deltas, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return new MemoryStream(bytes);
        }
    }
}
#endif
