// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Microsoft.AspNetCore.Watch.BrowserRefresh
{
    [TestClass]
    public class BlazorWasmHotReloadMiddlewareTest
    {
        private readonly ILogger<BlazorWasmHotReloadMiddleware> _logger;
        private BlazorWasmHotReloadMiddleware _middleware;

        public BlazorWasmHotReloadMiddlewareTest()
        {
            var loggerFactory = LoggerFactory.Create(_ => { });
            _logger = loggerFactory.CreateLogger<BlazorWasmHotReloadMiddleware>();
            _middleware = CreateMiddleware();
        }

        [TestMethod]
        public async Task DeltasAreSavedOnPost()
        {
            var context = new DefaultHttpContext();
            context.Request.Method = "post";
            context.Request.Headers.Origin = "http://localhost:5000";
            context.Request.ContentType = "application/json";
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

            await _middleware.InvokeAsync(context);

            AssertUpdates([update], _middleware.Updates);
        }

        [TestMethod]
        public async Task DuplicateDeltasOnPostAreIgnored()
        {
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
            context.Request.Headers.Origin = "http://localhost:5000";
            context.Request.ContentType = "application/json";
            context.Request.Body = GetJson(updates[0]);

            await _middleware.InvokeAsync(context);

            context = new DefaultHttpContext();
            context.Request.Method = "post";
            context.Request.Headers.Origin = "http://localhost:5000";
            context.Request.ContentType = "application/json";
            context.Request.Body = GetJson(updates[1]);
            await _middleware.InvokeAsync(context);

            AssertUpdates(updates, _middleware.Updates);
        }

        [TestMethod]
        public async Task MultipleDeltaPayloadsCanBeAccepted()
        {
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
            context.Request.Headers.Origin = "http://localhost:5000";
            context.Request.ContentType = "application/json";
            context.Request.Body = GetJson(update);
            await _middleware.InvokeAsync(context);

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
            context.Request.Headers.Origin = "http://localhost:5000";
            context.Request.ContentType = "application/json";
            context.Request.Body = GetJson(newUpdate);
            await _middleware.InvokeAsync(context);

            AssertUpdates([update, newUpdate], _middleware.Updates);
        }

        [TestMethod]
        public async Task Get_Returns204_IfNoDeltasPresent()
        {
            var context = new DefaultHttpContext();
            context.Request.Method = "get";
            
            await _middleware.InvokeAsync(context);

            Assert.AreEqual(204, context.Response.StatusCode);
        }

        [TestMethod]
        public async Task GetReturnsDeltas()
        {
            var context = new DefaultHttpContext();
            context.Request.Method = "get";
            var stream = new MemoryStream();
            context.Response.Body = stream;
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
            _middleware.Updates.AddRange(updates);

            await _middleware.InvokeAsync(context);

            Assert.AreEqual(200, context.Response.StatusCode);
            Assert.AreSequenceEqual(
                JsonSerializer.SerializeToUtf8Bytes(updates, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                stream.ToArray());
        }

        [TestMethod]
        public async Task InvokeAsync_AllowsOriginMatchingConfiguredUrls()
        {
            var middleware = CreateMiddleware("http://localhost:5000;https://localhost:5001");
            var context = new DefaultHttpContext();
            context.Request.Method = "get";
            context.Request.Headers.Origin = "http://localhost:5000";

            await middleware.InvokeAsync(context);

            Assert.AreEqual(StatusCodes.Status204NoContent, context.Response.StatusCode);
        }

        [TestMethod]
        public async Task InvokeAsync_RejectsPostFromUnknownOriginWithoutSavingDeltas()
        {
            var middleware = CreateMiddleware("http://localhost:5000");
            var context = new DefaultHttpContext();
            context.Request.Method = "post";
            context.Request.Headers.Origin = "http://evil.example:5000";
            context.Request.ContentType = "application/json";
            context.Request.Body = GetJson(new BlazorWasmHotReloadMiddleware.Update
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
            });

            await middleware.InvokeAsync(context);

            Assert.AreEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
            Assert.IsEmpty(middleware.Updates);
        }

        [TestMethod]
        [DataRow("http://localhost:5000", "http://localhost:5000", true)]
        [DataRow("http://localhost:5000;https://localhost:5001", "https://localhost:5001", true)]
        [DataRow("http://localhost:5000", "http://127.0.0.1:5000", true)]
        [DataRow("http://127.0.0.1:5000", "http://localhost:5000", true)]
        [DataRow("http://[::1]:5000", "http://localhost:5000", true)]
        [DataRow("http://localhost:5000", "http://evil.example:5000", false)]
        [DataRow("http://localhost:5000", "https://localhost:5000", false)]
        [DataRow("http://x:5000", "https://x:5000", false)]
        [DataRow("http://localhost:5000", "http://localhost:5001", false)]
        [DataRow("http://x:5000", "http://x:5001", false)]
        [DataRow("http://localhost:5000", "", false)]
        [DataRow("http://x:5000", "http://localhost:5000", false)]
        [DataRow("http://y:5000", "http://x:5000", false)]
        [DataRow("http://*:5000", "http://contoso.example:5000", false)] // wildcards in --urls are skipped
        [DataRow("http://+:5000", "http://contoso.example:5000", false)] // wildcards in --urls are skipped
        [DataRow("http://0.0.0.0:5000", "http://contoso.example:5000", false)] // wildcards in --urls are skipped
        [DataRow("http://[::]:5000", "http://contoso.example:5000", false)] // wildcards in --urls are skipped
        [DataRow("http://*:5000", "http://127.0.0.1:5000", true)] // wildcards in --urls allow loopback
        [DataRow("http://*:5000", "http://[::1]:5000", true)] // wildcards in --urls allow loopback
        [DataRow("http://*:5000", "http://localhost:5000", true)] // wildcards in --urls allow loopback
        public void IsAllowedOrigin_MatchesConfiguredServerUrls(string urls, string origin, bool allowed)
        {
            var addresses = BlazorWasmHotReloadMiddleware.ParseServerUrls(_logger, urls);
            var originHeader = origin.Length == 0 ? StringValues.Empty : new StringValues(origin);

            Assert.AreEqual(allowed, BlazorWasmHotReloadMiddleware.IsAllowedOrigin(originHeader, addresses));
        }

        private BlazorWasmHotReloadMiddleware CreateMiddleware(string? urls = null)
        {
            var configuration = new ConfigurationBuilder();
            if (urls != null)
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["urls"] = urls });
            }

            return new BlazorWasmHotReloadMiddleware(context => throw new TimeZoneNotFoundException(), _logger, configuration.Build());
        }

        private static void AssertUpdates(IReadOnlyList<BlazorWasmHotReloadMiddleware.Update> expected, IReadOnlyList<BlazorWasmHotReloadMiddleware.Update> actual)
        {
            Assert.HasCount(expected.Count, actual);

            for (var u = 0; u < expected.Count; u++)
            {
                var expectedUpdate = expected[u];
                var actualUpdate = actual[u];
                Assert.AreEqual(expectedUpdate.Id, actualUpdate.Id);
                Assert.HasCount(expectedUpdate.Deltas.Length, actualUpdate.Deltas);

                for (var i = 0; i < expectedUpdate.Deltas.Length; i++)
                {
                    Assert.AreEqual(expectedUpdate.Deltas[i].ILDelta, actualUpdate.Deltas[i].ILDelta);
                    Assert.AreEqual(expectedUpdate.Deltas[i].PdbDelta, actualUpdate.Deltas[i].PdbDelta);
                    Assert.AreEqual(expectedUpdate.Deltas[i].MetadataDelta, actualUpdate.Deltas[i].MetadataDelta);
                    Assert.AreEqual(expectedUpdate.Deltas[i].ModuleId, actualUpdate.Deltas[i].ModuleId);
                    Assert.AreSequenceEqual(expectedUpdate.Deltas[i].UpdatedTypes, actualUpdate.Deltas[i].UpdatedTypes);
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
