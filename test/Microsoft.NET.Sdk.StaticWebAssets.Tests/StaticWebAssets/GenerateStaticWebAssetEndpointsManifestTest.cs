// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.Json;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests.StaticWebAssets;

public class GenerateStaticWebAssetEndpointsManifestTest
{
    [Fact]
    public void GeneratesManifest_ForEndpointsWithTokens()
    {
        StaticWebAssetEndpoint[] expectedEndpoints =
        [
            new() {
                Route = "index.fingerprint.html",
                AssetFile = "index.html",
                Selectors = [],
                ResponseHeaders =
                [
                    new() {
                        Name = "Cache-Control",
                        Value = "max-age=31536000, immutable"
                    },
                    new() {
                        Name = "Content-Length",
                        Value = "10"
                    },
                    new() {
                        Name = "Content-Type",
                        Value = "text/html"
                    },
                    new() {
                        Name = "ETag",
                        Value = "\"integrity\""
                    },
                    new() {
                        Name = "Last-Modified",
                        Value = "Sat, 01 Jan 2000 00:00:01 GMT"
                    }
                ],
                EndpointProperties =
                [
                    new() {
                        Name = "fingerprint",
                        Value = "fingerprint"
                    },
                    new() {
                        Name = "integrity",
                        Value = "sha256-integrity"
                    },
                    new() {
                        Name = "label",
                        Value = "index.html"
                    }
                ]
            },
            new() {
                Route = "index.fingerprint.js",
                AssetFile = "index.fingerprint.js",
                Selectors = [],
                ResponseHeaders =
                [
                    new() {
                        Name = "Cache-Control",
                        Value = "max-age=31536000, immutable"
                    },
                    new() {
                        Name = "Content-Length",
                        Value = "10"
                    },
                    new() {
                        Name = "Content-Type",
                        Value = "text/javascript"
                    },
                    new() {
                        Name = "ETag",
                        Value = "\"integrity\""
                    },
                    new() {
                        Name = "Last-Modified",
                        Value = "Sat, 01 Jan 2000 00:00:01 GMT"
                    }
                ],
                EndpointProperties =
                [
                    new() {
                        Name = "fingerprint",
                        Value = "fingerprint"
                    },
                    new() {
                        Name = "integrity",
                        Value = "sha256-integrity"
                    },
                    new() {
                        Name = "label",
                        Value = "index.js"
                    }
                ]
            },
            new() {
                Route = "index.html",
                AssetFile = "index.html",
                Selectors = [],
                ResponseHeaders =
                [
                    new() {
                        Name = "Cache-Control",
                        Value = "no-cache"
                    },
                    new() {
                        Name = "Content-Length",
                        Value = "10"
                    },
                    new() {
                        Name = "Content-Type",
                        Value = "text/html"
                    },
                    new() {
                        Name = "ETag",
                        Value = "\"integrity\""
                    },
                    new() {
                        Name = "Last-Modified",
                        Value = "Sat, 01 Jan 2000 00:00:01 GMT"
                    }
                ],
                EndpointProperties = [
                    new() {
                        Name = "integrity",
                        Value = "sha256-integrity"
                    }]
            },
            new() {
                Route = "index.js",
                AssetFile = "index.fingerprint.js",
                Selectors = [],
                ResponseHeaders =
                [
                    new() {
                        Name = "Cache-Control",
                        Value = "no-cache"
                    },
                    new() {
                        Name = "Content-Length",
                        Value = "10"
                    },
                    new() {
                        Name = "Content-Type",
                        Value = "text/javascript"
                    },
                    new() {
                        Name = "ETag",
                        Value = "\"integrity\""
                    },
                    new() {
                        Name = "Last-Modified",
                        Value = "Sat, 01 Jan 2000 00:00:01 GMT"
                    }
                ],
                EndpointProperties = [
                    new() {
                        Name = "integrity",
                        Value = "sha256-integrity"
                    }]
            }
        ];
        Array.Sort(expectedEndpoints);

        var assets = new[]
        {
            CreateAsset("index.html", relativePath: "index#[.{fingerprint}]?.html"),
            CreateAsset("index.js", relativePath: "index#[.{fingerprint}]!.js"),
        };
        Array.Sort(assets, (l, r) => string.Compare(l.Identity, r.Identity, StringComparison.Ordinal));

        var endpoints = CreateEndpoints(assets);

        var path = Path.Combine(AppContext.BaseDirectory, Guid.NewGuid().ToString("N") + "endpoints.json");

        var task = new GenerateStaticWebAssetEndpointsManifest
        {
            Assets = assets.Select(a => a.ToTaskItem()).ToArray(),
            Endpoints = endpoints.Select(e => e.ToTaskItem()).ToArray(),
            ManifestType = "Build",
            Source = "MyApp",
            ManifestPath = path,
            BuildEngine = Mock.Of<IBuildEngine>()
        };

        try
        {
            // Act
            task.Execute();

            // Assert
            new FileInfo(path).Should().Exist();
            var manifest = File.ReadAllText(path);
            var json = JsonSerializer.Deserialize<StaticWebAssetEndpointsManifest>(manifest);
            json.Should().NotBeNull();
            json.Endpoints.Should().HaveCount(4);
            Array.Sort(json.Endpoints);
            json.Endpoints.Should().BeEquivalentTo(expectedEndpoints);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void ExcludesEndpoints_BasedOnExclusionPatterns()
    {
        // Arrange
        var assets = new[]
        {
            CreateAsset("index.html", relativePath: "index.html", basePath: "_content/MyApp"),
            CreateAsset("app.js", relativePath: "app.js", basePath: "_content/MyApp"),
            CreateAsset("styles.css", relativePath: "styles.css", basePath: "_content/OtherApp"),
        };
        Array.Sort(assets, (l, r) => string.Compare(l.Identity, r.Identity, StringComparison.Ordinal));

        var endpoints = CreateEndpoints(assets);
        var path = Path.Combine(AppContext.BaseDirectory, Guid.NewGuid().ToString("N") + "endpoints.json");
        var exclusionCachePath = Path.Combine(AppContext.BaseDirectory, Guid.NewGuid().ToString("N") + "exclusions.cache");

        var task = new GenerateStaticWebAssetEndpointsManifest
        {
            Assets = assets.Select(a => a.ToTaskItem()).ToArray(),
            Endpoints = endpoints.Select(e => e.ToTaskItem()).ToArray(),
            ManifestType = "Build",
            Source = "MyApp",
            ManifestPath = path,
            ExclusionPatterns = "**/*.js;**/*.html",
            ExclusionPatternsCacheFilePath = exclusionCachePath,
            BuildEngine = Mock.Of<IBuildEngine>()
        };

        try
        {
            // Act
            task.Execute();

            // Assert
            new FileInfo(path).Should().Exist();
            new FileInfo(exclusionCachePath).Should().Exist();

            var manifest = File.ReadAllText(path);
            var json = JsonSerializer.Deserialize<StaticWebAssetEndpointsManifest>(manifest);
            json.Should().NotBeNull();

            // Only styles.css endpoint should remain as others match _content/MyApp/**
            json.Endpoints.Should().HaveCount(1);
            json.Endpoints[0].Route.Should().Contain("styles.css");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            if (File.Exists(exclusionCachePath))
            {
                File.Delete(exclusionCachePath);
            }
        }
    }

    [Fact]
    public void SkipsRegeneration_WhenExclusionPatternsUnchanged()
    {
        // Arrange
        var assets = new[]
        {
            CreateAsset("index.html", relativePath: "index.html"),
        };

        var endpoints = CreateEndpoints(assets);
        var path = Path.Combine(AppContext.BaseDirectory, Guid.NewGuid().ToString("N") + "endpoints.json");
        var cachePath = Path.Combine(AppContext.BaseDirectory, Guid.NewGuid().ToString("N") + ".cache");
        var exclusionCachePath = Path.Combine(AppContext.BaseDirectory, Guid.NewGuid().ToString("N") + "exclusions.cache");

        // First run
        var task = new GenerateStaticWebAssetEndpointsManifest
        {
            Assets = assets.Select(a => a.ToTaskItem()).ToArray(),
            Endpoints = endpoints.Select(e => e.ToTaskItem()).ToArray(),
            ManifestType = "Build",
            Source = "MyApp",
            ManifestPath = path,
            CacheFilePath = cachePath,
            ExclusionPatterns = "test/**",
            ExclusionPatternsCacheFilePath = exclusionCachePath,
            BuildEngine = Mock.Of<IBuildEngine>()
        };

        try
        {
            // Act - First execution
            task.Execute();
            File.WriteAllText(cachePath, "cache"); // Simulate cache file
            var firstWriteTime = File.GetLastWriteTimeUtc(path);

            // Act - Second execution with same patterns
            Thread.Sleep(10); // Ensure time difference
            var task2 = new GenerateStaticWebAssetEndpointsManifest
            {
                Assets = assets.Select(a => a.ToTaskItem()).ToArray(),
                Endpoints = endpoints.Select(e => e.ToTaskItem()).ToArray(),
                ManifestType = "Build",
                Source = "MyApp",
                ManifestPath = path,
                CacheFilePath = cachePath,
                ExclusionPatterns = "test/**",
                ExclusionPatternsCacheFilePath = exclusionCachePath,
                BuildEngine = Mock.Of<IBuildEngine>()
            };
            task2.Execute();

            // Assert - File should not be regenerated
            var secondWriteTime = File.GetLastWriteTimeUtc(path);
            secondWriteTime.Should().Be(firstWriteTime);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            if (File.Exists(cachePath))
            {
                File.Delete(cachePath);
            }

            if (File.Exists(exclusionCachePath))
            {
                File.Delete(exclusionCachePath);
            }
        }
    }

    [Fact]
    public void RegeneratesManifest_WhenExclusionPatternsChange()
    {
        // Arrange
        var assets = new[]
        {
            CreateAsset("index.html", relativePath: "index.html"),
        };

        var endpoints = CreateEndpoints(assets);
        var endpointsManifestPath = Path.Combine(AppContext.BaseDirectory, Guid.NewGuid().ToString("N") + ".endpoints.json");
        var manifestPath = Path.Combine(AppContext.BaseDirectory, Guid.NewGuid().ToString("N") + ".cache");
        var exclusionCachePath = Path.Combine(AppContext.BaseDirectory, Guid.NewGuid().ToString("N") + ".exclusions.cache");

        // First run
        var task = new GenerateStaticWebAssetEndpointsManifest
        {
            Assets = assets.Select(a => a.ToTaskItem()).ToArray(),
            Endpoints = endpoints.Select(e => e.ToTaskItem()).ToArray(),
            ManifestType = "Build",
            Source = "MyApp",
            ManifestPath = endpointsManifestPath,
            CacheFilePath = manifestPath,
            ExclusionPatterns = "test/**",
            ExclusionPatternsCacheFilePath = exclusionCachePath,
            BuildEngine = Mock.Of<IBuildEngine>()
        };

        try
        {
            File.WriteAllText(manifestPath, "manifest");
            Thread.Sleep(10);

            // Act - First execution
            task.Execute();
            var firstWriteTime = File.GetLastWriteTimeUtc(endpointsManifestPath);

            // Act - Second execution with different patterns
            Thread.Sleep(10); // Ensure time difference
            var task2 = new GenerateStaticWebAssetEndpointsManifest
            {
                Assets = assets.Select(a => a.ToTaskItem()).ToArray(),
                Endpoints = endpoints.Select(e => e.ToTaskItem()).ToArray(),
                ManifestType = "Build",
                Source = "MyApp",
                ManifestPath = endpointsManifestPath,
                CacheFilePath = manifestPath,
                ExclusionPatterns = "different/**;pattern/**",
                ExclusionPatternsCacheFilePath = exclusionCachePath,
                BuildEngine = Mock.Of<IBuildEngine>()
            };
            task2.Execute();

            // Assert - File should be regenerated
            var secondWriteTime = File.GetLastWriteTimeUtc(endpointsManifestPath);
            secondWriteTime.Should().BeAfter(firstWriteTime);

            // Verify cache file was updated
            var cacheContent = File.ReadAllText(exclusionCachePath);
            cacheContent.Should().Contain("different/**");
            cacheContent.Should().Contain("pattern/**");
        }
        finally
        {
            if (File.Exists(endpointsManifestPath))
            {
                File.Delete(endpointsManifestPath);
            }

            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }

            if (File.Exists(exclusionCachePath))
            {
                File.Delete(exclusionCachePath);
            }
        }
    }

    private StaticWebAssetEndpoint[] CreateEndpoints(StaticWebAsset[] assets)
    {
        var defineStaticWebAssetEndpoints = new DefineStaticWebAssetEndpoints
        {
            CandidateAssets = assets.Select(a => a.ToTaskItem()).ToArray(),
            ExistingEndpoints = [],
            ContentTypeMappings = []
        };
        defineStaticWebAssetEndpoints.BuildEngine = Mock.Of<IBuildEngine>();

        defineStaticWebAssetEndpoints.Execute();
        return StaticWebAssetEndpoint.FromItemGroup(defineStaticWebAssetEndpoints.Endpoints);
    }

    private static StaticWebAsset CreateAsset(
        string itemSpec,
        string sourceId = "MyApp",
        string sourceType = "Discovered",
        string relativePath = null,
        string assetKind = "All",
        string assetMode = "All",
        string basePath = "base",
        string assetRole = "Primary",
        string relatedAsset = "",
        string assetTraitName = "",
        string assetTraitValue = "",
        string copyToOutputDirectory = "Never",
        string copytToPublishDirectory = "PreserveNewest")
    {
        var result = new StaticWebAsset()
        {
            Identity = Path.GetFullPath(itemSpec),
            SourceId = sourceId,
            SourceType = sourceType,
            ContentRoot = Directory.GetCurrentDirectory(),
            BasePath = basePath,
            RelativePath = relativePath ?? itemSpec,
            AssetKind = assetKind,
            AssetMode = assetMode,
            AssetRole = assetRole,
            AssetMergeBehavior = StaticWebAsset.MergeBehaviors.PreferTarget,
            AssetMergeSource = "",
            RelatedAsset = relatedAsset,
            AssetTraitName = assetTraitName,
            AssetTraitValue = assetTraitValue,
            CopyToOutputDirectory = copyToOutputDirectory,
            CopyToPublishDirectory = copytToPublishDirectory,
            OriginalItemSpec = itemSpec,
            // Add these to avoid accessing the disk to compute them
            Integrity = "integrity",
            Fingerprint = "fingerprint",
            FileLength = 10,
            LastWriteTime = new DateTime(2000, 1, 1, 0, 0, 1)
        };

        result.ApplyDefaults();
        result.Normalize();

        return result;
    }
}
