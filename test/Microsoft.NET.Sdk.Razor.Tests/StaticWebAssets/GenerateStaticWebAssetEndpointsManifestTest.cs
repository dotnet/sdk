// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Sdk.StaticWebAssets.Tasks;
using Moq;

namespace Microsoft.NET.Sdk.Razor.Tests.StaticWebAssets;

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
                        Name = "Accept-Ranges",
                        Value = "bytes"
                    },
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
                        Name = "Accept-Ranges",
                        Value = "bytes"
                    },
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
                        Value = "application/javascript"
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
                        Name = "Accept-Ranges",
                        Value = "bytes"
                    },
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
                        Name = "Accept-Ranges",
                        Value = "bytes"
                    },
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
                        Value = "application/javascript"
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

    private StaticWebAssetEndpoint[] CreateEndpoints(StaticWebAsset[] assets)
    {
        var defineStaticWebAssetEndpoints = new DefineStaticWebAssetEndpoints
        {
            CandidateAssets = assets.Select(a => a.ToTaskItem()).ToArray(),
            ExistingEndpoints = [],
            ContentTypeMappings = new TaskItem[]
            {
                    CreateContentMapping("*.html", "text/html"),
                    CreateContentMapping("*.js", "application/javascript"),
                    CreateContentMapping("*.css", "text/css")
            }
        };
        defineStaticWebAssetEndpoints.BuildEngine = Mock.Of<IBuildEngine>();
        defineStaticWebAssetEndpoints.TestLengthResolver = name => 10;
        defineStaticWebAssetEndpoints.TestLastWriteResolver = name => new DateTime(2000,1,1,0,0,1);

        defineStaticWebAssetEndpoints.Execute();
        return StaticWebAssetEndpoint.FromItemGroup(defineStaticWebAssetEndpoints.Endpoints);
    }

    private TaskItem CreateContentMapping(string pattern, string contentType)
    {
        return new TaskItem(contentType, new Dictionary<string, string>
            {
                { "Pattern", pattern },
                { "Priority", "0" }
            });
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
            Fingerprint = "fingerprint"
        };

        result.ApplyDefaults();
        result.Normalize();

        return result;
    }
}
