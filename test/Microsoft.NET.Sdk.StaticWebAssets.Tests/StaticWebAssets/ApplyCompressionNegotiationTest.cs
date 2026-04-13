// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests.StaticWebAssets;

public class ApplyCompressionNegotiationTest
{
    [Fact]
    public void AppliesContentNegotiationRules_ForExistingAssets()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var task = new ApplyCompressionNegotiation
        {
            BuildEngine = buildEngine.Object,
            CandidateAssets =
            [
                CreateCandidate(
                    Path.Combine("wwwroot", "candidate.js"),
                    "MyPackage",
                    "Discovered",
                    "candidate.js",
                    "All",
                    "All",
                    "original-fingerprint",
                    "original",
                    fileLength: 20
                ),
                CreateCandidate(
                    Path.Combine("compressed", "candidate.js.gz"),
                    "MyPackage",
                    "Discovered",
                    "candidate.js",
                    "All",
                    "All",
                    "compressed-fingerprint",
                    "compressed",
                    Path.Combine("wwwroot", "candidate.js"),
                    "Content-Encoding",
                    "gzip",
                    9
                )
            ],
            CandidateEndpoints =
            [
                CreateCandidateEndpoint(
                    "candidate.js",
                    Path.Combine("wwwroot", "candidate.js"),
                    CreateHeaders("text/javascript", [("Content-Length", "20")])),

                CreateCandidateEndpoint(
                    "candidate.js.gz",
                    Path.Combine("compressed", "candidate.js.gz"),
                    CreateHeaders("text/javascript", [("Content-Length", "9")]))
            ],
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().Be(true);
        var endpoints = StaticWebAssetEndpoint.FromItemGroup(task.UpdatedEndpoints);
        endpoints.Should().BeEquivalentTo((StaticWebAssetEndpoint[])[
            new ()
            {
                Route = "candidate.js",
                AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.gz")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Encoding", Value = "gzip" },
                    new () { Name = "Content-Length", Value = "9" },
                    new () { Name = "Content-Type", Value = "text/javascript" },
                    new () { Name = "Vary", Value = "Accept-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = [ new () { Name = "Content-Encoding", Value = "gzip", Quality = "1.0" } ],
            },
            new ()
            {
                Route = "candidate.js",
                AssetFile = Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Length", Value = "20" },
                    new () { Name = "Content-Type", Value = "text/javascript" },
                    new () { Name = "Vary", Value = "Accept-Encoding" },
                ],
                EndpointProperties = [],
                Selectors = [],
            },
            new ()
            {
                Route = "candidate.js.gz",
                AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.gz")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Encoding", Value = "gzip" },
                    new () { Name = "Content-Length", Value = "9" },
                    new () { Name = "Content-Type", Value = "text/javascript" },
                    new () { Name = "Vary", Value = "Accept-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = []
            }
        ]);
    }

    [Fact]
    public void AppliesContentNegotiationRules_ForExistingAssets_WithFingerprints()
    {
        var now = DateTime.Now;
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        List<ITaskItem> candidateAssets = [
            CreateCandidate(
                Path.Combine(AppContext.BaseDirectory, "wwwroot", "candidate.js"),
                "MyPackage",
                "Discovered",
                "candidate#[.{fingerprint}]?.js",
                "All",
                "All",
                "original-fingerprint",
                "original",
                fileLength: 20,
                lastModified: now
            )
        ];

        var compressedTask = new ResolveCompressedAssets
        {
            BuildEngine = buildEngine.Object,
            CandidateAssets = [.. candidateAssets],
            CompressionFormats = CreateCompressionFormats("gzip", "brotli"),
            Formats = "gzip;brotli",
            IncludePatterns = "*.js",
            OutputPath = AppContext.BaseDirectory
        };
        compressedTask.Execute().Should().BeTrue();

        var compressedAssets = compressedTask.AssetsToCompress;
        compressedAssets[0].SetMetadata(nameof(StaticWebAsset.Fingerprint), "gzip");
        compressedAssets[0].SetMetadata(nameof(StaticWebAsset.Integrity), "compressed-gzip");
        compressedAssets[0].SetMetadata(nameof(StaticWebAsset.FileLength), "9");
        compressedAssets[1].SetMetadata(nameof(StaticWebAsset.Fingerprint), "brotli");
        compressedAssets[1].SetMetadata(nameof(StaticWebAsset.Integrity), "compressed-brotli");
        compressedAssets[1].SetMetadata(nameof(StaticWebAsset.FileLength), "7");
        candidateAssets.AddRange(compressedAssets);
        var expectedName = Path.GetFileNameWithoutExtension(compressedAssets[0].ItemSpec);
        var defineStaticAssetEndpointsTask = new DefineStaticWebAssetEndpoints
        {
            BuildEngine = buildEngine.Object,
            CandidateAssets = [.. candidateAssets],
            ExistingEndpoints = [],
            ContentTypeMappings = []
        };
        defineStaticAssetEndpointsTask.Execute().Should().BeTrue();
        var compressed = defineStaticAssetEndpointsTask.Endpoints;

        var task = new ApplyCompressionNegotiation
        {
            BuildEngine = buildEngine.Object,
            CandidateAssets = [.. candidateAssets],
            CandidateEndpoints = compressed,
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().Be(true);
        var endpoints = StaticWebAssetEndpoint.FromItemGroup(task.UpdatedEndpoints);
        var expectedEndpoints = new StaticWebAssetEndpoint[]
        {
            new()
            {
                Route = "candidate.fingerprint.js",
                AssetFile = Path.Combine(AppContext.BaseDirectory, $"{expectedName}.br"),
                Selectors = [
                    new ()
                    {
                            Name = "Content-Encoding",
                            Value = "br",
                            Quality = "1.0"
                    }
                ],
            ResponseHeaders = [            new ()
            {
                Name = "Cache-Control",
                Value = "max-age=31536000, immutable"
            },
            new ()
            {
                Name = "Content-Encoding",
                Value = "br"
            },
            new ()
            {
                Name = "Content-Length",
                Value = "7"
            },
            new ()
            {
                Name = "Content-Type",
                Value = "text/javascript"
            },
            new ()
            {
                Name = "ETag",
                Value = "\u0022compressed-brotli\u0022"
            },
            new ()
            {
                Name = "Last-Modified",
                Value = now.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture)
            },
            new ()
            {
                Name = "Vary",
                Value = "Accept-Encoding"
            }
        ],
        EndpointProperties = [
            new ()
            {
                Name = "fingerprint",
                Value = "fingerprint"
            },
            new ()
            {
                Name = "integrity",
                Value = "sha256-original"
            },
            new ()
            {
                Name = "label",
                Value = "candidate.js"
            }
            ]
        },
            new()
            {
                Route = "candidate.fingerprint.js",
                AssetFile = Path.Combine(AppContext.BaseDirectory, $"{expectedName}.gz"),
                Selectors = [
                new ()
                {
                    Name = "Content-Encoding",
                    Value = "gzip",
                Quality = "0.9"
                }
            ],
            ResponseHeaders = [                new ()
                {
                    Name = "Cache-Control",
                    Value = "max-age=31536000, immutable"
                },
                new ()
                {
                    Name = "Content-Encoding",
                    Value = "gzip"
                },
                new ()
                {
                    Name = "Content-Length",
                    Value = "9"
                },
                new ()
                {
                    Name = "Content-Type",
                    Value = "text/javascript"
                },
                new ()
                {
                    Name = "ETag",
                    Value = "\u0022compressed-gzip\u0022"
                },
                new ()
                {
                    Name = "Last-Modified",
                    Value = now.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture)
                },
                new ()
                {
                    Name = "Vary",
                    Value = "Accept-Encoding"
                }
            ],
            EndpointProperties = [
                new ()
                {
                    Name = "fingerprint",
                    Value = "fingerprint"
                },
                new ()
                {
                    Name = "integrity",
                    Value = "sha256-original"
                },
                new ()
                {
                    Name = "label",
                    Value = "candidate.js"
                }
                ]
            },
            new()
            {
                Route = "candidate.fingerprint.js",
                AssetFile = Path.Combine(AppContext.BaseDirectory, "wwwroot", "candidate.js"),
                ResponseHeaders = [                new ()
                {
                    Name = "Cache-Control",
                    Value = "max-age=31536000, immutable"
                },
                new ()
                {
                    Name = "Content-Length",
                    Value = "20"
                },
                new ()
                {
                    Name = "Content-Type",
                    Value = "text/javascript"
                },
                new ()
                {
                    Name = "ETag",
                    Value = "\u0022original\u0022"
                },
                new ()
                {
                    Name = "Last-Modified",
                    Value = now.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture)
                },
                new ()
                {
                    Name = "Vary",
                    Value = "Accept-Encoding"
                }
            ],
            EndpointProperties = [
                    new ()
                    {
                        Name = "fingerprint",
                        Value = "fingerprint"
                    },
                    new ()
                    {
                        Name = "integrity",
                        Value = "sha256-original"
                    },
                    new ()
                    {
                        Name = "label",
                        Value = "candidate.js"
                    }
                ]
            },
            new()
            {
                Route = "candidate.fingerprint.js.br",
                AssetFile = Path.Combine(AppContext.BaseDirectory, $"{expectedName}.br"),
                ResponseHeaders = [                new ()
                {
                    Name = "Cache-Control",
                    Value = "max-age=31536000, immutable"
                },
                new ()
                {
                    Name = "Content-Encoding",
                    Value = "br"
                },
                new ()
                {
                    Name = "Content-Length",
                    Value = "7"
                },
                new ()
                {
                    Name = "Content-Type",
                    Value = "text/javascript"
                },
                new ()
                {
                    Name = "ETag",
                    Value = "\u0022compressed-brotli\u0022"
                },
                new ()
                {
                    Name = "Last-Modified",
                    Value = now.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture)
                },
                new ()
                {
                    Name = "Vary",
                    Value = "Accept-Encoding"
                }
            ],
            EndpointProperties = [
                new ()
                {
                    Name = "fingerprint",
                    Value = "fingerprint"
                },
                new ()
                {
                    Name = "integrity",
                    Value = "sha256-compressed-brotli"
                },
                new ()
                {
                    Name = "label",
                    Value = "candidate.js.br"
                }
                ]
            },
            new()
            {
                Route = "candidate.fingerprint.js.gz",
                AssetFile = Path.Combine(AppContext.BaseDirectory, $"{expectedName}.gz"),
                ResponseHeaders = [                new ()
                {
                    Name = "Cache-Control",
                    Value = "max-age=31536000, immutable"
                },
                new ()
                {
                    Name = "Content-Encoding",
                    Value = "gzip"
                },
                new ()
                {
                    Name = "Content-Length",
                    Value = "9"
                },
                new ()
                {
                    Name = "Content-Type",
                    Value = "text/javascript"
                },
                new ()
                {
                    Name = "ETag",
                    Value = "\u0022compressed-gzip\u0022"
                },
                new ()
                {
                    Name = "Last-Modified",
                    Value = now.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture)
                },
                new ()
                {
                    Name = "Vary",
                    Value = "Accept-Encoding"
                }
            ],
            EndpointProperties = [
                new ()
                {
                    Name = "fingerprint",
                    Value = "fingerprint"
                },
                new ()
                {
                    Name = "integrity",
                    Value = "sha256-compressed-gzip"
                },
                new ()
                {
                    Name = "label",
                    Value = "candidate.js.gz"
                }
                ]
            },
            new()
            {
                Route = "candidate.js",
                AssetFile = Path.Combine(AppContext.BaseDirectory, $"{expectedName}.br"),
                Selectors = [
                new ()
                {
                    Name = "Content-Encoding",
                    Value = "br",
                Quality = "1.0"
                }
            ],
            ResponseHeaders = [                new ()
                {
                    Name = "Cache-Control",
                    Value = "no-cache"
                },
                new ()
                {
                    Name = "Content-Encoding",
                    Value = "br"
                },
                new ()
                {
                    Name = "Content-Length",
                    Value = "7"
                },
                new ()
                {
                    Name = "Content-Type",
                    Value = "text/javascript"
                },
                new ()
                {
                    Name = "ETag",
                    Value = "\u0022compressed-brotli\u0022"
                },
                new ()
                {
                    Name = "Last-Modified",
                    Value = now.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture)
                },
                new ()
                {
                    Name = "Vary",
                    Value = "Accept-Encoding"
                }
            ],
            EndpointProperties = [
                new ()
                {
                    Name = "integrity",
                    Value = "sha256-original"
                }
                ]
            },
            new()
            {
                Route = "candidate.js",
                AssetFile = Path.Combine(AppContext.BaseDirectory, $"{expectedName}.gz"),
                Selectors = [
                new ()
                {
                    Name = "Content-Encoding",
                    Value = "gzip",
                Quality = "0.9"
                }
            ],
            ResponseHeaders = [                new ()
                {
                    Name = "Cache-Control",
                    Value = "no-cache"
                },
                new ()
                {
                    Name = "Content-Encoding",
                    Value = "gzip"
                },
                new ()
                {
                    Name = "Content-Length",
                    Value = "9"
                },
                new ()
                {
                    Name = "Content-Type",
                    Value = "text/javascript"
                },
                new ()
                {
                    Name = "ETag",
                    Value = "\u0022compressed-gzip\u0022"
                },
                new ()
                {
                    Name = "Last-Modified",
                    Value = now.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture)
                },
                new ()
                {
                    Name = "Vary",
                    Value = "Accept-Encoding"
                }
            ],
            EndpointProperties = [
                new ()
                {
                    Name = "integrity",
                    Value = "sha256-original"
                }
                ]
            },
            new()
            {
                Route = "candidate.js",
                AssetFile = Path.Combine(AppContext.BaseDirectory, "wwwroot", "candidate.js"),
                ResponseHeaders = [                new ()
                {
                    Name = "Cache-Control",
                    Value = "no-cache"
                },
                new ()
                {
                    Name = "Content-Length",
                    Value = "20"
                },
                new ()
                {
                    Name = "Content-Type",
                    Value = "text/javascript"
                },
                new ()
                {
                    Name = "ETag",
                    Value = "\u0022original\u0022"
                },
                new ()
                {
                    Name = "Last-Modified",
                    Value = now.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture)
                },
                new ()
                {
                    Name = "Vary",
                    Value = "Accept-Encoding"
                }
            ],
            EndpointProperties = [
                new ()
                {
                    Name = "integrity",
                    Value = "sha256-original"
                }
                ]
            },
            new()
            {
                Route = "candidate.js.br",
                AssetFile = Path.Combine(AppContext.BaseDirectory, $"{expectedName}.br"),
                ResponseHeaders = [                new ()
                {
                    Name = "Cache-Control",
                    Value = "no-cache"
                },
                new ()
                {
                    Name = "Content-Encoding",
                    Value = "br"
                },
                new ()
                {
                    Name = "Content-Length",
                    Value = "7"
                },
                new ()
                {
                    Name = "Content-Type",
                    Value = "text/javascript"
                },
                new ()
                {
                    Name = "ETag",
                    Value = "\u0022compressed-brotli\u0022"
                },
                new ()
                {
                    Name = "Last-Modified",
                    Value = now.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture)
                },
                new ()
                {
                    Name = "Vary",
                    Value = "Accept-Encoding"
                }
            ],
            EndpointProperties = [
                new ()
                {
                    Name = "integrity",
                    Value = "sha256-compressed-brotli"
                }
                ]
            },
            new()
            {
                Route = "candidate.js.gz",
                AssetFile = Path.Combine(AppContext.BaseDirectory, $"{expectedName}.gz"),
                ResponseHeaders = [                new () {
                Name = "Cache-Control",
                    Value = "no-cache"
                },
                new () {
                Name = "Content-Encoding",
                    Value = "gzip"
                },
                new () {
                Name = "Content-Length",
                    Value = "9"
                },
                new () {
                Name = "Content-Type",
                    Value = "text/javascript"
                },
                new () {
                Name = "ETag",
                    Value = "\u0022compressed-gzip\u0022"
                },
                new () {
                Name = "Last-Modified",
                    Value = now.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture)
                },
                new () {
                Name = "Vary",
                    Value = "Accept-Encoding"
                }
            ],
            EndpointProperties = [
                new () {
                    Name = "integrity",
                    Value = "sha256-compressed-gzip"
                }
                ]
            }
};

        endpoints.Should().BeEquivalentTo(expectedEndpoints);
    }

    [Fact]
    public void AppliesContentNegotiationRules_ToAllRelatedAssetEndpoints()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var task = new ApplyCompressionNegotiation
        {
            BuildEngine = buildEngine.Object,
            CandidateAssets =
            [
                CreateCandidate(
                    Path.Combine("wwwroot", "candidate.js"),
                    "MyPackage",
                    "Discovered",
                    "candidate.js",
                    "All",
                    "All",
                    "original-fingerprint",
                    "original",
                    fileLength: 20
                ),
                CreateCandidate(
                    Path.Combine("compressed", "candidate.js.gz"),
                    "MyPackage",
                    "Discovered",
                    "candidate.js",
                    "All",
                    "All",
                    "compressed-fingerprint",
                    "compressed",
                    Path.Combine("wwwroot", "candidate.js"),
                    "Content-Encoding",
                    "gzip",
                    fileLength: 9
                )
            ],
            CandidateEndpoints =
            [
                CreateCandidateEndpoint(
                    "candidate.js",
                    Path.Combine("wwwroot", "candidate.js"),
                    CreateHeaders("text/javascript")),

                CreateCandidateEndpoint(
                    "candidate.fingerprint.js",
                    Path.Combine("wwwroot", "candidate.js"),
                    CreateHeaders("text/javascript")),

                CreateCandidateEndpoint(
                    "candidate.js.gz",
                    Path.Combine("compressed", "candidate.js.gz"),
                    CreateHeaders("text/javascript"))
            ],
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().Be(true);
        var endpoints = StaticWebAssetEndpoint.FromItemGroup(task.UpdatedEndpoints);
        endpoints.Should().BeEquivalentTo((StaticWebAssetEndpoint[])[
            new ()
            {
                Route = "candidate.js",
                AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.gz")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Encoding", Value = "gzip" },
                    new () { Name = "Content-Type", Value = "text/javascript" },
                    new () { Name = "Vary", Value = "Accept-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = [ new () { Name = "Content-Encoding", Value = "gzip", Quality = "1.0" } ],
            },
            new ()
            {
                Route = "candidate.js",
                AssetFile = Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Type", Value = "text/javascript" },
                    new () { Name = "Vary", Value = "Accept-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = [],
            },
            new ()
            {
                Route = "candidate.fingerprint.js",
                AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.gz")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Encoding", Value = "gzip" },
                    new () { Name = "Content-Type", Value = "text/javascript" },
                    new () { Name = "Vary", Value = "Accept-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = [ new () { Name = "Content-Encoding", Value = "gzip", Quality = "1.0" } ],
            },
            new ()
            {
                Route = "candidate.fingerprint.js",
                AssetFile = Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Type", Value = "text/javascript" },
                    new () { Name = "Vary", Value = "Accept-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = [],
            },
            new ()
            {
                Route = "candidate.js.gz",
                AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.gz")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Encoding", Value = "gzip" },
                    new () { Name = "Content-Type", Value = "text/javascript" },
                    new () { Name = "Vary", Value = "Accept-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = []
            }
        ]);
    }

    [Fact]
    public void AppliesContentNegotiationRules_IgnoresAlreadyProcessedEndpoints()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var task = new ApplyCompressionNegotiation
        {
            BuildEngine = buildEngine.Object,
            CandidateAssets =
            [
                CreateCandidate(
                    Path.Combine("wwwroot", "candidate.js"),
                    "MyPackage",
                    "Discovered",
                    "candidate.js",
                    "All",
                    "All",
                    "original-fingerprint",
                    "original"
                ),
                CreateCandidate(
                    Path.Combine("compressed", "candidate.js.gz"),
                    "MyPackage",
                    "Discovered",
                    "candidate.js",
                    "All",
                    "All",
                    "compressed-fingerprint",
                    "compressed",
                    Path.Combine("wwwroot", "candidate.js"),
                    "Content-Encoding",
                    "gzip"
                )
            ],
            CandidateEndpoints = new StaticWebAssetEndpoint[]
            {
                new()
                {
                        Route = "candidate.js",
                        AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.gz")),
                        ResponseHeaders =
                    [
                        new () { Name = "Content-Encoding", Value = "gzip" },
                        new (){ Name = "Content-Type", Value = "text/javascript" },
                        new (){ Name = "Vary", Value = "Accept-Encoding" }
                    ],
                    EndpointProperties = [],
                    Selectors = [ new StaticWebAssetEndpointSelector { Name = "Content-Encoding", Value = "gzip", Quality = "1.0" } ],
                },
                new()
                {
                        Route = "candidate.js",
                        AssetFile = Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")),
                        ResponseHeaders =
                    [
                        new (){ Name = "Content-Type", Value = "text/javascript" }
                    ],
                    EndpointProperties = [],
                    Selectors = [],
                },
                new()
                {
                        Route = "candidate.fingerprint.js",
                        AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.gz")),
                        ResponseHeaders =
                    [
                        new (){ Name = "Content-Encoding", Value = "gzip" },
                        new (){ Name = "Content-Type", Value = "text/javascript" },
                        new (){ Name = "Vary", Value = "Accept-Encoding" }
                    ],
                    EndpointProperties = [],
                    Selectors = [ new () { Name = "Content-Encoding", Value = "gzip", Quality = "1.0" } ],
                },
                new()
                {
                        Route = "candidate.fingerprint.js",
                        AssetFile = Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")),
                        ResponseHeaders =
                    [
                        new () { Name = "Content-Type", Value = "text/javascript" }
                    ],
                    EndpointProperties = [],
                    Selectors = [],
                },
                new()
                {
                        Route = "candidate.js.gz",
                        AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.gz")),
                        ResponseHeaders =
                    [
                        new () { Name = "Content-Encoding", Value = "gzip" },
                        new () { Name = "Content-Type", Value = "text/javascript" },
                        new () { Name = "Vary", Value = "Accept-Encoding" }
                    ],
                    EndpointProperties = [],
                    Selectors = []
                }
            }.Select(e => e.ToTaskItem()).ToArray(),
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().Be(true);
        var endpoints = StaticWebAssetEndpoint.FromItemGroup(task.UpdatedEndpoints);
        endpoints.Should().BeEquivalentTo([
            new StaticWebAssetEndpoint
            {
                Route = "candidate.js",
                AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.gz")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Encoding", Value = "gzip" },
                    new () { Name = "Content-Type", Value = "text/javascript" },
                    new () { Name = "Vary", Value = "Accept-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = [ new StaticWebAssetEndpointSelector { Name = "Content-Encoding", Value = "gzip", Quality = "1.0" } ],
            },
            new StaticWebAssetEndpoint
            {
                Route = "candidate.js",
                AssetFile = Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Type", Value = "text/javascript" },
                    new () { Name = "Vary", Value = "Accept-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = [],
            },
            new StaticWebAssetEndpoint
            {
                Route = "candidate.fingerprint.js",
                AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.gz")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Encoding", Value = "gzip" },
                    new () { Name = "Content-Type", Value = "text/javascript" },
                    new () { Name = "Vary", Value = "Accept-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = [ new StaticWebAssetEndpointSelector { Name = "Content-Encoding", Value = "gzip", Quality = "1.0" } ],
            },
            new StaticWebAssetEndpoint
            {
                Route = "candidate.fingerprint.js",
                AssetFile = Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Type", Value = "text/javascript" },
                    new () { Name = "Vary", Value = "Accept-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = [],
            },
            new StaticWebAssetEndpoint
            {
                Route = "candidate.js.gz",
                AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.gz")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Encoding", Value = "gzip" },
                    new () { Name = "Content-Type", Value = "text/javascript" },
                    new () { Name = "Vary", Value = "Accept-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = []
            }
        ]);
    }

    [Fact]
    public void AppliesContentNegotiationRules_ProcessesNewCompressedFormatsWhenAvailable()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var task = new ApplyCompressionNegotiation
        {
            BuildEngine = buildEngine.Object,
            CandidateAssets =
            [
                CreateCandidate(
                    Path.Combine("wwwroot", "candidate.js"),
                    "MyPackage",
                    "Discovered",
                    "candidate.js",
                    "All",
                    "All",
                    "original-fingerprint",
                    "original",
                    fileLength: 20
                ),
                CreateCandidate(
                    Path.Combine("compressed", "candidate.js.gz"),
                    "MyPackage",
                    "Discovered",
                    "candidate.js",
                    "All",
                    "All",
                    "compressed-gzip",
                    "compressed",
                    Path.Combine("wwwroot", "candidate.js"),
                    "Content-Encoding",
                    "gzip",
                    fileLength: 9
                ),
                CreateCandidate(
                    Path.Combine("compressed", "candidate.js.br"),
                    "MyPackage",
                    "Discovered",
                    "candidate.js",
                    "All",
                    "All",
                    "compressed-brotli",
                    "compressed",
                    Path.Combine("wwwroot", "candidate.js"),
                    "Content-Encoding",
                    "br",
                    fileLength: 9
                )
            ],
            CandidateEndpoints = new StaticWebAssetEndpoint[]
            {
                new()
                {
                        Route = "candidate.js",
                        AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.gz")),
                        ResponseHeaders =
                    [
                        new () { Name = "Content-Encoding", Value = "gzip" },
                        new (){ Name = "Content-Type", Value = "text/javascript" },
                        new (){ Name = "Vary", Value = "Accept-Encoding" }
                    ],
                    EndpointProperties = [],
                    Selectors = [ new StaticWebAssetEndpointSelector { Name = "Content-Encoding", Value = "gzip", Quality = "1.0" } ],
                },
                new()
                {
                        Route = "candidate.js",
                        AssetFile = Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")),
                        ResponseHeaders =
                    [
                        new (){ Name = "Content-Type", Value = "text/javascript" }
                    ],
                    EndpointProperties = [],
                    Selectors = [],
                },
                new()
                {
                        Route = "candidate.fingerprint.js",
                        AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.gz")),
                        ResponseHeaders =
                    [
                        new (){ Name = "Content-Encoding", Value = "gzip" },
                        new (){ Name = "Content-Type", Value = "text/javascript" },
                        new (){ Name = "Vary", Value = "Accept-Encoding" }
                    ],
                    EndpointProperties = [],
                    Selectors = [ new () { Name = "Content-Encoding", Value = "gzip", Quality = "1.0" } ],
                },
                new()
                {
                        Route = "candidate.fingerprint.js",
                        AssetFile = Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")),
                        ResponseHeaders =
                    [
                        new () { Name = "Content-Type", Value = "text/javascript" }
                    ],
                    EndpointProperties = [],
                    Selectors = [],
                },
                new()
                {
                        Route = "candidate.js.gz",
                        AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.gz")),
                        ResponseHeaders =
                    [
                        new () { Name = "Content-Encoding", Value = "gzip" },
                        new () { Name = "Content-Type", Value = "text/javascript" },
                        new () { Name = "Vary", Value = "Accept-Encoding" }
                    ],
                    EndpointProperties = [],
                    Selectors = []
                },
                new()
                {
                        Route = "candidate.js.br",
                        AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.br")),
                        ResponseHeaders =
                    [
                        new () { Name = "Content-Type", Value = "text/javascript" },
                    ],
                    EndpointProperties = [],
                    Selectors = []
                }
            }.Select(e => e.ToTaskItem()).ToArray(),
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().Be(true);
        var endpoints = StaticWebAssetEndpoint.FromItemGroup(task.UpdatedEndpoints);
        endpoints.Should().BeEquivalentTo([
            new StaticWebAssetEndpoint
            {
                Route = "candidate.js",
                AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.gz")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Encoding", Value = "gzip" },
                    new () { Name = "Content-Type", Value = "text/javascript" },
                    new () { Name = "Vary", Value = "Accept-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = [ new StaticWebAssetEndpointSelector { Name = "Content-Encoding", Value = "gzip", Quality = "0.9" } ],
            },
            new StaticWebAssetEndpoint
            {
                Route = "candidate.js",
                AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.br")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Encoding", Value = "br" },
                    new () { Name = "Content-Type", Value = "text/javascript" },
                    new () { Name = "Vary", Value = "Accept-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = [ new StaticWebAssetEndpointSelector { Name = "Content-Encoding", Value = "br", Quality = "1.0" } ],
            },
            new StaticWebAssetEndpoint
            {
                Route = "candidate.js",
                AssetFile = Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Type", Value = "text/javascript" },
                    new () { Name = "Vary", Value = "Accept-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = [],
            },
            new StaticWebAssetEndpoint
            {
                Route = "candidate.fingerprint.js",
                AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.gz")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Encoding", Value = "gzip" },
                    new () { Name = "Content-Type", Value = "text/javascript" },
                    new () { Name = "Vary", Value = "Accept-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = [ new StaticWebAssetEndpointSelector { Name = "Content-Encoding", Value = "gzip", Quality = "0.9" } ],
            },
            new StaticWebAssetEndpoint
            {
                Route = "candidate.fingerprint.js",
                AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.br")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Encoding", Value = "br" },
                    new () { Name = "Content-Type", Value = "text/javascript" },
                    new () { Name = "Vary", Value = "Accept-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = [ new StaticWebAssetEndpointSelector { Name = "Content-Encoding", Value = "br", Quality = "1.0" } ],
            },
            new StaticWebAssetEndpoint
            {
                Route = "candidate.fingerprint.js",
                AssetFile = Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Type", Value = "text/javascript" },
                    new () { Name = "Vary", Value = "Accept-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = [],
            },
            new StaticWebAssetEndpoint
            {
                Route = "candidate.js.gz",
                AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.gz")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Encoding", Value = "gzip" },
                    new () { Name = "Content-Type", Value = "text/javascript" },
                    new () { Name = "Vary", Value = "Accept-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = []
            },
                        new StaticWebAssetEndpoint
            {
                Route = "candidate.js.br",
                AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.br")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Encoding", Value = "br" },
                    new () { Name = "Content-Type", Value = "text/javascript" },
                    new () { Name = "Vary", Value = "Accept-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = []
            }
        ]);
    }

    [Fact]
    public void AppliesContentNegotiationRules_AddsVaryHeaderToEndpointsWithSameRouteButDifferentAssets()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var task = new ApplyCompressionNegotiation
        {
            BuildEngine = buildEngine.Object,
            CandidateAssets =
            [
                CreateCandidate(
                    Path.Combine("wwwroot", "candidate.js"),
                    "MyPackage",
                    "Discovered",
                    "candidate.js",
                    "All",
                    "All",
                    "original-fingerprint",
                    "original",
                    fileLength: 20
                ),
                CreateCandidate(
                    Path.Combine("compressed", "candidate.js.gz"),
                    "MyPackage",
                    "Discovered",
                    "candidate.js",
                    "All",
                    "All",
                    "compressed-fingerprint",
                    "compressed",
                    Path.Combine("wwwroot", "candidate.js"),
                    "Content-Encoding",
                    "gzip",
                    9
                ),
                // This represents a different asset (e.g., a publish asset) that shares the same route
                // but wasn't part of the compression processing
                CreateCandidate(
                    Path.Combine("publish", "candidate.js"),
                    "PublishPackage",
                    "Discovered",
                    "candidate.js",
                    "Publish",
                    "All",
                    "publish-fingerprint",
                    "publish",
                    fileLength: 18
                )
            ],
            CandidateEndpoints =
            [
                CreateCandidateEndpoint(
                    "candidate.js",
                    Path.Combine("wwwroot", "candidate.js"),
                    CreateHeaders("text/javascript", [("Content-Length", "20")])),

                CreateCandidateEndpoint(
                    "candidate.js.gz",
                    Path.Combine("compressed", "candidate.js.gz"),
                    CreateHeaders("text/javascript", [("Content-Length", "9")])),

                // This endpoint shares the route but points to a different asset
                CreateCandidateEndpoint(
                    "candidate.js",
                    Path.Combine("publish", "candidate.js"),
                    CreateHeaders("text/javascript", [("Content-Length", "18")]))
            ],
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().Be(true);
        var endpoints = StaticWebAssetEndpoint.FromItemGroup(task.UpdatedEndpoints);
        endpoints.Should().BeEquivalentTo((StaticWebAssetEndpoint[])[
            new ()
            {
                Route = "candidate.js",
                AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.gz")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Encoding", Value = "gzip" },
                    new () { Name = "Content-Length", Value = "9" },
                    new () { Name = "Content-Type", Value = "text/javascript" },
                    new () { Name = "Vary", Value = "Accept-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = [ new () { Name = "Content-Encoding", Value = "gzip", Quality = "1.0" } ],
            },
            new ()
            {
                Route = "candidate.js",
                AssetFile = Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Length", Value = "20" },
                    new () { Name = "Content-Type", Value = "text/javascript" },
                    new () { Name = "Vary", Value = "Accept-Encoding" },
                ],
                EndpointProperties = [],
                Selectors = [],
            },
            new ()
            {
                Route = "candidate.js.gz",
                AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.gz")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Encoding", Value = "gzip" },
                    new () { Name = "Content-Length", Value = "9" },
                    new () { Name = "Content-Type", Value = "text/javascript" },
                    new () { Name = "Vary", Value = "Accept-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = []
            },
            new ()
            {
                Route = "candidate.js",
                AssetFile = Path.GetFullPath(Path.Combine("publish", "candidate.js")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Length", Value = "18" },
                    new () { Name = "Content-Type", Value = "text/javascript" },
                    new () { Name = "Vary", Value = "Accept-Encoding" },
                ],
                EndpointProperties = [],
                Selectors = [],
            }
        ]);
    }

    private static StaticWebAssetEndpointResponseHeader[] CreateHeaders(string contentType, params (string name, string value)[] AdditionalHeaders)
    {
        return
        [
            new StaticWebAssetEndpointResponseHeader {
                Name = "Content-Type",
                    Value = contentType
            },
            ..(AdditionalHeaders ?? []).Select(h => new StaticWebAssetEndpointResponseHeader { Name = h.name, Value = h.value })
        ];
    }

    private static ITaskItem CreateCandidate(
        string itemSpec,
        string sourceId,
        string sourceType,
        string relativePath,
        string assetKind,
        string assetMode,
        string fingerprint = "",
        string integrity = "",
        string relatedAsset = "",
        string assetTraitName = "",
        string assetTraitValue = "",
        long fileLength = 9,
        DateTimeOffset? lastModified = null)
    {
        lastModified ??= new DateTimeOffset(2023, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var result = new StaticWebAsset()
        {
            Identity = Path.GetFullPath(itemSpec),
            SourceId = sourceId,
            SourceType = sourceType,
            ContentRoot = Directory.GetCurrentDirectory(),
            BasePath = "base",
            RelativePath = relativePath,
            AssetKind = assetKind,
            AssetMode = assetMode,
            AssetRole = "Primary",
            RelatedAsset = relatedAsset,
            AssetTraitName = assetTraitName,
            AssetTraitValue = assetTraitValue,
            CopyToOutputDirectory = "",
            CopyToPublishDirectory = "",
            OriginalItemSpec = itemSpec,
            // Add these to avoid accessing the disk to compute them
            Integrity = integrity,
            Fingerprint = "fingerprint",
            FileLength = fileLength,
            LastWriteTime = lastModified.Value,
        };

        result.ApplyDefaults();
        result.Normalize();

        return result.ToTaskItem();
    }

    private static ITaskItem CreateCandidateEndpoint(
        string route,
        string assetFile,
        StaticWebAssetEndpointResponseHeader[] responseHeaders = null,
        StaticWebAssetEndpointSelector[] responseSelector = null,
        StaticWebAssetEndpointProperty[] properties = null)
    {
        return new StaticWebAssetEndpoint
        {
            Route = route,
            AssetFile = Path.GetFullPath(assetFile),
            ResponseHeaders = responseHeaders ?? [],
            EndpointProperties = properties ?? [],
            Selectors = responseSelector ?? []
        }.ToTaskItem();
    }

    [Fact]
    public void AppliesContentNegotiationRules_AttachesWeakETagAsResponseHeader()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var task = new ApplyCompressionNegotiation
        {
            BuildEngine = buildEngine.Object,
            AttachWeakETagToCompressedAssets = "ResponseHeader",
            CandidateAssets =
            [
                CreateCandidate(
                    Path.Combine("wwwroot", "candidate.js"),
                    "MyPackage",
                    "Discovered",
                    "candidate.js",
                    "All",
                    "All",
                    "original-fingerprint",
                    "original",
                    fileLength: 20
                ),
                CreateCandidate(
                    Path.Combine("compressed", "candidate.js.gz"),
                    "MyPackage",
                    "Discovered",
                    "candidate.js",
                    "All",
                    "All",
                    "compressed-fingerprint",
                    "compressed",
                    Path.Combine("wwwroot", "candidate.js"),
                    "Content-Encoding",
                    "gzip",
                    9
                )
            ],
            CandidateEndpoints =
            [
                CreateCandidateEndpoint(
                    "candidate.js",
                    Path.Combine("wwwroot", "candidate.js"),
                    CreateHeaders("text/javascript", [("Content-Length", "20"), ("ETag", "\"original-etag\"")])),

                CreateCandidateEndpoint(
                    "candidate.js.gz",
                    Path.Combine("compressed", "candidate.js.gz"),
                    CreateHeaders("text/javascript", [("Content-Length", "9"), ("ETag", "\"compressed-etag\"")]))
            ],
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().Be(true);
        var endpoints = StaticWebAssetEndpoint.FromItemGroup(task.UpdatedEndpoints);

        // The compressed endpoint for the original route should have the weak ETag from the original
        var compressedEndpoint = endpoints.FirstOrDefault(e => e.Route == "candidate.js" && e.AssetFile.Contains("candidate.js.gz"));
        compressedEndpoint.Should().NotBeNull();
        compressedEndpoint.ResponseHeaders.Should().Contain(h => h.Name == "ETag" && h.Value == "W/\"original-etag\"");
    }

    [Fact]
    public void AppliesContentNegotiationRules_AttachesWeakETagAsEndpointProperty()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var task = new ApplyCompressionNegotiation
        {
            BuildEngine = buildEngine.Object,
            AttachWeakETagToCompressedAssets = "EndpointProperty",
            CandidateAssets =
            [
                CreateCandidate(
                    Path.Combine("wwwroot", "candidate.js"),
                    "MyPackage",
                    "Discovered",
                    "candidate.js",
                    "All",
                    "All",
                    "original-fingerprint",
                    "original",
                    fileLength: 20
                ),
                CreateCandidate(
                    Path.Combine("compressed", "candidate.js.gz"),
                    "MyPackage",
                    "Discovered",
                    "candidate.js",
                    "All",
                    "All",
                    "compressed-fingerprint",
                    "compressed",
                    Path.Combine("wwwroot", "candidate.js"),
                    "Content-Encoding",
                    "gzip",
                    9
                )
            ],
            CandidateEndpoints =
            [
                CreateCandidateEndpoint(
                    "candidate.js",
                    Path.Combine("wwwroot", "candidate.js"),
                    CreateHeaders("text/javascript", [("Content-Length", "20"), ("ETag", "\"original-etag\"")])),

                CreateCandidateEndpoint(
                    "candidate.js.gz",
                    Path.Combine("compressed", "candidate.js.gz"),
                    CreateHeaders("text/javascript", [("Content-Length", "9"), ("ETag", "\"compressed-etag\"")]))
            ],
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().Be(true);
        var endpoints = StaticWebAssetEndpoint.FromItemGroup(task.UpdatedEndpoints);

        // The compressed endpoint for the original route should have the original-resource property
        var compressedEndpoint = endpoints.FirstOrDefault(e => e.Route == "candidate.js" && e.AssetFile.Contains("candidate.js.gz"));
        compressedEndpoint.Should().NotBeNull();
        compressedEndpoint.EndpointProperties.Should().Contain(p => p.Name == "original-resource" && p.Value == "\"original-etag\"");
    }

    [Fact]
    public void AppliesContentNegotiationRules_DoesNotAttachETagWhenModeIsEmpty()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var task = new ApplyCompressionNegotiation
        {
            BuildEngine = buildEngine.Object,
            AttachWeakETagToCompressedAssets = "", // Empty string should not attach ETag
            CandidateAssets =
            [
                CreateCandidate(
                    Path.Combine("wwwroot", "candidate.js"),
                    "MyPackage",
                    "Discovered",
                    "candidate.js",
                    "All",
                    "All",
                    "original-fingerprint",
                    "original",
                    fileLength: 20
                ),
                CreateCandidate(
                    Path.Combine("compressed", "candidate.js.gz"),
                    "MyPackage",
                    "Discovered",
                    "candidate.js",
                    "All",
                    "All",
                    "compressed-fingerprint",
                    "compressed",
                    Path.Combine("wwwroot", "candidate.js"),
                    "Content-Encoding",
                    "gzip",
                    9
                )
            ],
            CandidateEndpoints =
            [
                CreateCandidateEndpoint(
                    "candidate.js",
                    Path.Combine("wwwroot", "candidate.js"),
                    CreateHeaders("text/javascript", [("Content-Length", "20"), ("ETag", "\"original-etag\"")])),

                CreateCandidateEndpoint(
                    "candidate.js.gz",
                    Path.Combine("compressed", "candidate.js.gz"),
                    CreateHeaders("text/javascript", [("Content-Length", "9"), ("ETag", "\"compressed-etag\"")]))
            ],
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().Be(true);
        var endpoints = StaticWebAssetEndpoint.FromItemGroup(task.UpdatedEndpoints);

        // The compressed endpoint for the original route should not have weak ETag or original-resource property
        var compressedEndpoint = endpoints.FirstOrDefault(e => e.Route == "candidate.js" && e.AssetFile.Contains("candidate.js.gz"));
        compressedEndpoint.Should().NotBeNull();
        compressedEndpoint.ResponseHeaders.Should().NotContain(h => h.Name == "ETag" && h.Value.StartsWith("W/"));
        compressedEndpoint.EndpointProperties.Should().NotContain(p => p.Name == "original-resource");
    }

    private static ITaskItem[] CreateCompressionFormats(params string[] formatNames)
    {
        var formats = new Dictionary<string, (string Extension, string ContentEncoding, bool UsesDictionary)>(StringComparer.OrdinalIgnoreCase)
        {
            ["gzip"] = (".gz", "gzip", false),
            ["brotli"] = (".br", "br", false),
            ["zstd"] = (".zst", "zstd", false),
            ["dcz"] = (".dcz", "dcz", true),
        };

        return formatNames.Select(name =>
        {
            var (ext, enc, usesDict) = formats[name];
            var item = new TaskItem(name);
            item.SetMetadata("FileExtension", ext);
            item.SetMetadata("ContentEncoding", enc);
            if (usesDict)
            {
                item.SetMetadata("UsesDictionary", "true");
            }
            return (ITaskItem)item;
        }).ToArray();
    }

    [Fact]
    public void AssignsDescendingQuality_ForMultipleFormats_ByFileSize()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var task = new ApplyCompressionNegotiation
        {
            BuildEngine = buildEngine.Object,
            CandidateAssets =
            [
                CreateCandidate(
                    Path.Combine("wwwroot", "candidate.js"),
                    "MyPackage", "Discovered", "candidate.js", "All", "All",
                    "original-fp", "original", fileLength: 20),
                CreateCandidate(
                    Path.Combine("compressed", "candidate.js.gz"),
                    "MyPackage", "Discovered", "candidate.js", "All", "All",
                    "gz-fp", "compressed",
                    Path.Combine("wwwroot", "candidate.js"),
                    "Content-Encoding", "gzip", fileLength: 9),
                CreateCandidate(
                    Path.Combine("compressed", "candidate.js.br"),
                    "MyPackage", "Discovered", "candidate.js", "All", "All",
                    "br-fp", "compressed",
                    Path.Combine("wwwroot", "candidate.js"),
                    "Content-Encoding", "br", fileLength: 7),
                CreateCandidate(
                    Path.Combine("compressed", "candidate.js.zst"),
                    "MyPackage", "Discovered", "candidate.js", "All", "All",
                    "zst-fp", "compressed",
                    Path.Combine("wwwroot", "candidate.js"),
                    "Content-Encoding", "zstd", fileLength: 5)
            ],
            CandidateEndpoints =
            [
                CreateCandidateEndpoint("candidate.js", Path.Combine("wwwroot", "candidate.js"),
                    CreateHeaders("text/javascript", [("Content-Length", "20")])),
                CreateCandidateEndpoint("candidate.js.gz", Path.Combine("compressed", "candidate.js.gz"),
                    CreateHeaders("text/javascript", [("Content-Length", "9")])),
                CreateCandidateEndpoint("candidate.js.br", Path.Combine("compressed", "candidate.js.br"),
                    CreateHeaders("text/javascript", [("Content-Length", "7")])),
                CreateCandidateEndpoint("candidate.js.zst", Path.Combine("compressed", "candidate.js.zst"),
                    CreateHeaders("text/javascript", [("Content-Length", "5")])),
            ],
        };

        var result = task.Execute();

        result.Should().Be(true);
        var endpoints = StaticWebAssetEndpoint.FromItemGroup(task.UpdatedEndpoints);

        // zstd(5) < brotli(7) < gzip(9) → zstd="1.0", brotli="0.9", gzip="0.8"
        var zstdEndpoint = endpoints.First(e => e.Route == "candidate.js" && e.AssetFile.Contains("candidate.js.zst"));
        zstdEndpoint.Selectors.Should().Contain(s => s.Name == "Content-Encoding" && s.Value == "zstd" && s.Quality == "1.0");

        var brEndpoint = endpoints.First(e => e.Route == "candidate.js" && e.AssetFile.Contains("candidate.js.br"));
        brEndpoint.Selectors.Should().Contain(s => s.Name == "Content-Encoding" && s.Value == "br" && s.Quality == "0.9");

        var gzEndpoint = endpoints.First(e => e.Route == "candidate.js" && e.AssetFile.Contains("candidate.js.gz"));
        gzEndpoint.Selectors.Should().Contain(s => s.Name == "Content-Encoding" && s.Value == "gzip" && s.Quality == "0.8");
    }

    [Fact]
    public void AssignsQuality_WithTiebreaking_ByFormatPriority()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        // Both have same file size (9), but CompressionFormats orders zstd before gzip
        var task = new ApplyCompressionNegotiation
        {
            BuildEngine = buildEngine.Object,
            CompressionFormats = CreateCompressionFormats("zstd", "gzip"),
            CandidateAssets =
            [
                CreateCandidate(
                    Path.Combine("wwwroot", "candidate.js"),
                    "MyPackage", "Discovered", "candidate.js", "All", "All",
                    "original-fp", "original", fileLength: 20),
                CreateCandidate(
                    Path.Combine("compressed", "candidate.js.gz"),
                    "MyPackage", "Discovered", "candidate.js", "All", "All",
                    "gz-fp", "compressed",
                    Path.Combine("wwwroot", "candidate.js"),
                    "Content-Encoding", "gzip", fileLength: 9),
                CreateCandidate(
                    Path.Combine("compressed", "candidate.js.zst"),
                    "MyPackage", "Discovered", "candidate.js", "All", "All",
                    "zst-fp", "compressed",
                    Path.Combine("wwwroot", "candidate.js"),
                    "Content-Encoding", "zstd", fileLength: 9)
            ],
            CandidateEndpoints =
            [
                CreateCandidateEndpoint("candidate.js", Path.Combine("wwwroot", "candidate.js"),
                    CreateHeaders("text/javascript", [("Content-Length", "20")])),
                CreateCandidateEndpoint("candidate.js.gz", Path.Combine("compressed", "candidate.js.gz"),
                    CreateHeaders("text/javascript", [("Content-Length", "9")])),
                CreateCandidateEndpoint("candidate.js.zst", Path.Combine("compressed", "candidate.js.zst"),
                    CreateHeaders("text/javascript", [("Content-Length", "9")])),
            ],
        };

        var result = task.Execute();

        result.Should().Be(true);
        var endpoints = StaticWebAssetEndpoint.FromItemGroup(task.UpdatedEndpoints);

        // Same size → tiebreak by format priority: zstd (index 0) before gzip (index 1)
        var zstdEndpoint = endpoints.First(e => e.Route == "candidate.js" && e.AssetFile.Contains("candidate.js.zst"));
        zstdEndpoint.Selectors.Should().Contain(s => s.Name == "Content-Encoding" && s.Value == "zstd" && s.Quality == "1.0");

        var gzEndpoint = endpoints.First(e => e.Route == "candidate.js" && e.AssetFile.Contains("candidate.js.gz"));
        gzEndpoint.Selectors.Should().Contain(s => s.Name == "Content-Encoding" && s.Value == "gzip" && s.Quality == "0.9");
    }

    [Fact]
    public void AssignsQuality_WithoutCompressionFormats_TiebreaksAlphabetically()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        // No CompressionFormats set — same size variants tiebreak alphabetically by encoding
        var task = new ApplyCompressionNegotiation
        {
            BuildEngine = buildEngine.Object,
            CandidateAssets =
            [
                CreateCandidate(
                    Path.Combine("wwwroot", "candidate.js"),
                    "MyPackage", "Discovered", "candidate.js", "All", "All",
                    "original-fp", "original", fileLength: 20),
                CreateCandidate(
                    Path.Combine("compressed", "candidate.js.gz"),
                    "MyPackage", "Discovered", "candidate.js", "All", "All",
                    "gz-fp", "compressed",
                    Path.Combine("wwwroot", "candidate.js"),
                    "Content-Encoding", "gzip", fileLength: 9),
                CreateCandidate(
                    Path.Combine("compressed", "candidate.js.zst"),
                    "MyPackage", "Discovered", "candidate.js", "All", "All",
                    "zst-fp", "compressed",
                    Path.Combine("wwwroot", "candidate.js"),
                    "Content-Encoding", "zstd", fileLength: 9)
            ],
            CandidateEndpoints =
            [
                CreateCandidateEndpoint("candidate.js", Path.Combine("wwwroot", "candidate.js"),
                    CreateHeaders("text/javascript", [("Content-Length", "20")])),
                CreateCandidateEndpoint("candidate.js.gz", Path.Combine("compressed", "candidate.js.gz"),
                    CreateHeaders("text/javascript", [("Content-Length", "9")])),
                CreateCandidateEndpoint("candidate.js.zst", Path.Combine("compressed", "candidate.js.zst"),
                    CreateHeaders("text/javascript", [("Content-Length", "9")])),
            ],
        };

        var result = task.Execute();

        result.Should().Be(true);
        var endpoints = StaticWebAssetEndpoint.FromItemGroup(task.UpdatedEndpoints);

        // Same size, no format priority → alphabetical: "gzip" < "zstd"
        var gzEndpoint = endpoints.First(e => e.Route == "candidate.js" && e.AssetFile.Contains("candidate.js.gz"));
        gzEndpoint.Selectors.Should().Contain(s => s.Name == "Content-Encoding" && s.Value == "gzip" && s.Quality == "1.0");

        var zstdEndpoint = endpoints.First(e => e.Route == "candidate.js" && e.AssetFile.Contains("candidate.js.zst"));
        zstdEndpoint.Selectors.Should().Contain(s => s.Name == "Content-Encoding" && s.Value == "zstd" && s.Quality == "0.9");
    }

    [Fact]
    public void AssignsQuality_ForMultipleResources_IndependentRanking()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var task = new ApplyCompressionNegotiation
        {
            BuildEngine = buildEngine.Object,
            CandidateAssets =
            [
                // Resource A
                CreateCandidate(
                    Path.Combine("wwwroot", "a.js"),
                    "MyPackage", "Discovered", "a.js", "All", "All",
                    "a-fp", "original", fileLength: 20),
                CreateCandidate(
                    Path.Combine("compressed", "a.js.gz"),
                    "MyPackage", "Discovered", "a.js", "All", "All",
                    "a-gz-fp", "compressed",
                    Path.Combine("wwwroot", "a.js"),
                    "Content-Encoding", "gzip", fileLength: 9),
                CreateCandidate(
                    Path.Combine("compressed", "a.js.br"),
                    "MyPackage", "Discovered", "a.js", "All", "All",
                    "a-br-fp", "compressed",
                    Path.Combine("wwwroot", "a.js"),
                    "Content-Encoding", "br", fileLength: 7),
                // Resource B (only gzip)
                CreateCandidate(
                    Path.Combine("wwwroot", "b.js"),
                    "MyPackage", "Discovered", "b.js", "All", "All",
                    "b-fp", "original", fileLength: 30),
                CreateCandidate(
                    Path.Combine("compressed", "b.js.gz"),
                    "MyPackage", "Discovered", "b.js", "All", "All",
                    "b-gz-fp", "compressed",
                    Path.Combine("wwwroot", "b.js"),
                    "Content-Encoding", "gzip", fileLength: 15),
            ],
            CandidateEndpoints =
            [
                CreateCandidateEndpoint("a.js", Path.Combine("wwwroot", "a.js"),
                    CreateHeaders("text/javascript", [("Content-Length", "20")])),
                CreateCandidateEndpoint("a.js.gz", Path.Combine("compressed", "a.js.gz"),
                    CreateHeaders("text/javascript", [("Content-Length", "9")])),
                CreateCandidateEndpoint("a.js.br", Path.Combine("compressed", "a.js.br"),
                    CreateHeaders("text/javascript", [("Content-Length", "7")])),
                CreateCandidateEndpoint("b.js", Path.Combine("wwwroot", "b.js"),
                    CreateHeaders("text/javascript", [("Content-Length", "30")])),
                CreateCandidateEndpoint("b.js.gz", Path.Combine("compressed", "b.js.gz"),
                    CreateHeaders("text/javascript", [("Content-Length", "15")])),
            ],
        };

        var result = task.Execute();

        result.Should().Be(true);
        var endpoints = StaticWebAssetEndpoint.FromItemGroup(task.UpdatedEndpoints);

        // Resource A: brotli(7)="1.0", gzip(9)="0.9"
        var aBr = endpoints.First(e => e.Route == "a.js" && e.AssetFile.Contains("a.js.br"));
        aBr.Selectors.Should().Contain(s => s.Name == "Content-Encoding" && s.Value == "br" && s.Quality == "1.0");

        var aGz = endpoints.First(e => e.Route == "a.js" && e.AssetFile.Contains("a.js.gz"));
        aGz.Selectors.Should().Contain(s => s.Name == "Content-Encoding" && s.Value == "gzip" && s.Quality == "0.9");

        // Resource B: only gzip → "1.0" (independent ranking)
        var bGz = endpoints.First(e => e.Route == "b.js" && e.AssetFile.Contains("b.js.gz"));
        bGz.Selectors.Should().Contain(s => s.Name == "Content-Encoding" && s.Value == "gzip" && s.Quality == "1.0");
    }

    [Fact]
    public void AssignsQuality_WithDescendingTieredSeries_ForManyFormats()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        // Create 12 compressed variants with different sizes to test tiered quality series
        var candidateAssets = new List<ITaskItem>
        {
            CreateCandidate(
                Path.Combine("wwwroot", "candidate.js"),
                "MyPackage", "Discovered", "candidate.js", "All", "All",
                "original-fp", "original", fileLength: 100)
        };
        var candidateEndpoints = new List<ITaskItem>
        {
            CreateCandidateEndpoint("candidate.js", Path.Combine("wwwroot", "candidate.js"),
                CreateHeaders("text/javascript", [("Content-Length", "100")]))
        };

        for (var i = 0; i < 12; i++)
        {
            var encoding = $"enc{i:D2}";
            var ext = $".e{i:D2}";
            var size = 10 + i; // 10, 11, 12, ... 21 — all different sizes
            candidateAssets.Add(CreateCandidate(
                Path.Combine("compressed", $"candidate.js{ext}"),
                "MyPackage", "Discovered", "candidate.js", "All", "All",
                $"fp{i}", "compressed",
                Path.Combine("wwwroot", "candidate.js"),
                "Content-Encoding", encoding, fileLength: size));
            candidateEndpoints.Add(CreateCandidateEndpoint(
                $"candidate.js{ext}", Path.Combine("compressed", $"candidate.js{ext}"),
                CreateHeaders("text/javascript", [("Content-Length", size.ToString())])));
        }

        var task = new ApplyCompressionNegotiation
        {
            BuildEngine = buildEngine.Object,
            CandidateAssets = candidateAssets.ToArray(),
            CandidateEndpoints = candidateEndpoints.ToArray(),
        };

        var result = task.Execute();

        result.Should().Be(true);
        var endpoints = StaticWebAssetEndpoint.FromItemGroup(task.UpdatedEndpoints);

        // Rank 0 (size=10): "1.0", rank 1: "0.9", ..., rank 9 (size=19): "0.1"
        // Rank 10 (size=20): "0.09", rank 11 (size=21): "0.08"
        var first = endpoints.First(e => e.Route == "candidate.js" && e.AssetFile.Contains("candidate.js.e00"));
        first.Selectors.Should().Contain(s => s.Name == "Content-Encoding" && s.Quality == "1.0");

        var ninth = endpoints.First(e => e.Route == "candidate.js" && e.AssetFile.Contains("candidate.js.e08"));
        ninth.Selectors.Should().Contain(s => s.Name == "Content-Encoding" && s.Quality == "0.2");

        var tenth = endpoints.First(e => e.Route == "candidate.js" && e.AssetFile.Contains("candidate.js.e09"));
        tenth.Selectors.Should().Contain(s => s.Name == "Content-Encoding" && s.Quality == "0.1");

        // Tier 2: ranks 10+ get two decimal places
        var eleventh = endpoints.First(e => e.Route == "candidate.js" && e.AssetFile.Contains("candidate.js.e10"));
        eleventh.Selectors.Should().Contain(s => s.Name == "Content-Encoding" && s.Quality == "0.09");

        var twelfth = endpoints.First(e => e.Route == "candidate.js" && e.AssetFile.Contains("candidate.js.e11"));
        twelfth.Selectors.Should().Contain(s => s.Name == "Content-Encoding" && s.Quality == "0.08");
    }

    [Theory]
    [InlineData(0, "1.0")]
    [InlineData(1, "0.9")]
    [InlineData(9, "0.1")]
    [InlineData(10, "0.09")]
    [InlineData(18, "0.01")]
    [InlineData(19, "0.009")]
    [InlineData(27, "0.001")]
    [InlineData(28, "0.0009")]
    public void ComputeQualityValue_ProducesCorrectSeries(int rank, string expected)
    {
        ApplyCompressionNegotiation.ComputeQualityValue(rank).Should().Be(expected);
    }

    [Fact]
    public void RouteCollisions_IncludeCollateralEndpoints()
    {
        // Two different assets (build and publish) that share a route, only the build one has gzip.
        // The publish endpoint should appear in the output with a Vary header as a collateral member.
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var buildAssetPath = Path.Combine("wwwroot", "build", "app.css");
        var publishAssetPath = Path.Combine("wwwroot", "publish", "app.css");
        var compressedAssetPath = Path.Combine("compressed", "app.css.gz");

        var task = new ApplyCompressionNegotiation
        {
            BuildEngine = buildEngine.Object,
            CandidateAssets =
            [
                CreateCandidate(buildAssetPath, "MyPackage", "Discovered", "css/app.css", "All", "All",
                    integrity: "build-integrity", fileLength: 100),
                CreateCandidate(publishAssetPath, "MyPackage", "Discovered", "css/app.css", "All", "All",
                    integrity: "publish-integrity", fileLength: 100),
                CreateCandidate(compressedAssetPath, "MyPackage", "Discovered", "css/app.css.gz", "All", "All",
                    integrity: "compressed-integrity",
                    relatedAsset: Path.GetFullPath(buildAssetPath),
                    assetTraitName: "Content-Encoding", assetTraitValue: "gzip", fileLength: 50),
            ],
            CandidateEndpoints =
            [
                // Both primary endpoints and compressed endpoint share fingerprint="fp" for compatibility
                CreateCandidateEndpoint("css/app.css", buildAssetPath,
                    [new() { Name = "Content-Length", Value = "100" }, new() { Name = "Content-Type", Value = "text/css" }],
                    properties: [new() { Name = "fingerprint", Value = "fp" }]),
                CreateCandidateEndpoint("css/app.css", publishAssetPath,
                    [new() { Name = "Content-Length", Value = "100" }, new() { Name = "Content-Type", Value = "text/css" }],
                    properties: [new() { Name = "fingerprint", Value = "fp" }]),
                CreateCandidateEndpoint("css/app.css.gz", compressedAssetPath,
                    [new() { Name = "Content-Length", Value = "50" }, new() { Name = "Content-Type", Value = "text/css" }],
                    properties: [new() { Name = "fingerprint", Value = "fp" }]),
            ],
            CompressionFormats = CreateCompressionFormats("gzip"),
        };

        var result = task.Execute();
        result.Should().BeTrue();

        var endpoints = StaticWebAssetEndpoint.FromItemGroup(task.UpdatedEndpoints);
        // Should include: compressed endpoint (modified), build original (with Vary), publish original (collateral with Vary),
        // and synthetic (compressed served at original's route with Content-Encoding selector)
        endpoints.Should().Contain(e =>
            e.Route == "css/app.css" && e.AssetFile == Path.GetFullPath(publishAssetPath),
            "publish endpoint should be included as a collateral member of the modified route group");
        // The publish endpoint should have a Vary header
        endpoints.First(e => e.Route == "css/app.css" && e.AssetFile == Path.GetFullPath(publishAssetPath))
            .ResponseHeaders.Should().Contain(h => h.Name == "Vary" && h.Value == "Accept-Encoding");
    }

    [Fact]
    public void MultipleCompressedAssets_ProduceNoDuplicateSynthetics()
    {
        // One original with both gzip and brotli — verify no duplicate synthetic endpoints at the same route.
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var originalPath = Path.Combine("wwwroot", "app.js");
        var gzPath = Path.Combine("compressed", "app.js.gz");
        var brPath = Path.Combine("compressed", "app.js.br");

        var task = new ApplyCompressionNegotiation
        {
            BuildEngine = buildEngine.Object,
            CandidateAssets =
            [
                CreateCandidate(originalPath, "MyPackage", "Discovered", "app.js", "All", "All",
                    integrity: "orig-integrity", fileLength: 100),
                CreateCandidate(gzPath, "MyPackage", "Discovered", "app.js.gz", "All", "All",
                    integrity: "gz-integrity",
                    relatedAsset: Path.GetFullPath(originalPath),
                    assetTraitName: "Content-Encoding", assetTraitValue: "gzip", fileLength: 60),
                CreateCandidate(brPath, "MyPackage", "Discovered", "app.js.br", "All", "All",
                    integrity: "br-integrity",
                    relatedAsset: Path.GetFullPath(originalPath),
                    assetTraitName: "Content-Encoding", assetTraitValue: "br", fileLength: 40),
            ],
            CandidateEndpoints =
            [
                // All endpoints share fingerprint="fp" for compatibility
                CreateCandidateEndpoint("app.js", originalPath,
                    [new() { Name = "Content-Length", Value = "100" }, new() { Name = "Content-Type", Value = "text/javascript" }],
                    properties: [new() { Name = "fingerprint", Value = "fp" }]),
                CreateCandidateEndpoint("app.js.gz", gzPath,
                    [new() { Name = "Content-Length", Value = "60" }, new() { Name = "Content-Type", Value = "text/javascript" }],
                    properties: [new() { Name = "fingerprint", Value = "fp" }]),
                CreateCandidateEndpoint("app.js.br", brPath,
                    [new() { Name = "Content-Length", Value = "40" }, new() { Name = "Content-Type", Value = "text/javascript" }],
                    properties: [new() { Name = "fingerprint", Value = "fp" }]),
            ],
            CompressionFormats = CreateCompressionFormats("gzip", "brotli"),
        };

        var result = task.Execute();
        result.Should().BeTrue();

        var endpoints = StaticWebAssetEndpoint.FromItemGroup(task.UpdatedEndpoints);
        // Count synthetic endpoints at the original's route
        var syntheticsAtOriginalRoute = endpoints.Where(e =>
            e.Route == "app.js" && e.Selectors.Any(s => s.Name == "Content-Encoding")).ToList();
        syntheticsAtOriginalRoute.Should().HaveCount(2, "should have one synthetic per compression format");
        syntheticsAtOriginalRoute.Select(e => e.Selectors.First(s => s.Name == "Content-Encoding").Value)
            .Should().BeEquivalentTo(["br", "gzip"]);
    }

    [Fact]
    public void ModifiedRouteGroup_EmitsAllGroupMembers()
    {
        // Route group with 3 endpoints from different assets — only one has compression.
        // All 3 should appear in output when the group is modified.
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var asset1Path = Path.Combine("wwwroot", "a1", "shared.js");
        var asset2Path = Path.Combine("wwwroot", "a2", "shared.js");
        var asset3Path = Path.Combine("wwwroot", "a3", "shared.js");
        var compressedPath = Path.Combine("compressed", "shared.js.gz");

        var task = new ApplyCompressionNegotiation
        {
            BuildEngine = buildEngine.Object,
            CandidateAssets =
            [
                CreateCandidate(asset1Path, "MyPackage", "Discovered", "shared.js", "All", "All",
                    integrity: "int1", fileLength: 100),
                CreateCandidate(asset2Path, "MyPackage", "Discovered", "shared.js", "All", "All",
                    integrity: "int2", fileLength: 100),
                CreateCandidate(asset3Path, "MyPackage", "Discovered", "shared.js", "All", "All",
                    integrity: "int3", fileLength: 100),
                CreateCandidate(compressedPath, "MyPackage", "Discovered", "shared.js.gz", "All", "All",
                    integrity: "cint",
                    relatedAsset: Path.GetFullPath(asset1Path),
                    assetTraitName: "Content-Encoding", assetTraitValue: "gzip", fileLength: 50),
            ],
            CandidateEndpoints =
            [
                // All endpoints share fingerprint="fp" for compatibility
                CreateCandidateEndpoint("shared.js", asset1Path,
                    [new() { Name = "Content-Length", Value = "100" }, new() { Name = "Content-Type", Value = "text/javascript" }],
                    properties: [new() { Name = "fingerprint", Value = "fp" }]),
                CreateCandidateEndpoint("shared.js", asset2Path,
                    [new() { Name = "Content-Length", Value = "100" }, new() { Name = "Content-Type", Value = "text/javascript" }],
                    properties: [new() { Name = "fingerprint", Value = "fp" }]),
                CreateCandidateEndpoint("shared.js", asset3Path,
                    [new() { Name = "Content-Length", Value = "100" }, new() { Name = "Content-Type", Value = "text/javascript" }],
                    properties: [new() { Name = "fingerprint", Value = "fp" }]),
                CreateCandidateEndpoint("shared.js.gz", compressedPath,
                    [new() { Name = "Content-Length", Value = "50" }, new() { Name = "Content-Type", Value = "text/javascript" }],
                    properties: [new() { Name = "fingerprint", Value = "fp" }]),
            ],
            CompressionFormats = CreateCompressionFormats("gzip"),
        };

        var result = task.Execute();
        result.Should().BeTrue();

        var endpoints = StaticWebAssetEndpoint.FromItemGroup(task.UpdatedEndpoints);
        // All 3 primary endpoints at "shared.js" should appear (they share the modified route group)
        var primaryRouteEndpoints = endpoints.Where(e => e.Route == "shared.js").ToList();
        primaryRouteEndpoints.Select(e => e.AssetFile).Should().Contain(Path.GetFullPath(asset1Path));
        primaryRouteEndpoints.Select(e => e.AssetFile).Should().Contain(Path.GetFullPath(asset2Path));
        primaryRouteEndpoints.Select(e => e.AssetFile).Should().Contain(Path.GetFullPath(asset3Path));
        // All 3 should have Vary headers
        foreach (var ep in primaryRouteEndpoints.Where(e => !e.Selectors.Any(s => s.Name == "Content-Encoding")))
        {
            ep.ResponseHeaders.Should().Contain(h => h.Name == "Vary" && h.Value == "Accept-Encoding");
        }
    }

    [Fact]
    public void DictionaryFormat_AddsDualSelectorsAndVaryHeader()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var originalPath = Path.Combine("wwwroot", "candidate.js");
        var dczPath = Path.Combine("compressed", "candidate.js.dcz");

        var task = new ApplyCompressionNegotiation
        {
            BuildEngine = buildEngine.Object,
            CandidateAssets =
            [
                CreateCandidate(
                    originalPath,
                    "MyPackage",
                    "Discovered",
                    "candidate.js",
                    "All",
                    "All",
                    "original-fingerprint",
                    "original",
                    fileLength: 20
                ),
                CreateCandidate(
                    dczPath,
                    "MyPackage",
                    "Discovered",
                    "candidate.js",
                    "All",
                    "All",
                    "dcz-fingerprint",
                    "compressed",
                    Path.Combine("wwwroot", "candidate.js"),
                    "Content-Encoding",
                    "dcz",
                    5
                )
            ],
            CandidateEndpoints =
            [
                CreateCandidateEndpoint(
                    "candidate.js",
                    originalPath,
                    CreateHeaders("text/javascript", [("Content-Length", "20")])),
                CreateCandidateEndpoint(
                    "candidate.js.dcz",
                    dczPath,
                    CreateHeaders("text/javascript", [("Content-Length", "5")]))
            ],
            CompressionFormats = CreateCompressionFormats("dcz"),
            DictionaryCandidates =
            [
                CreateDictionaryCandidate(Path.GetFullPath(originalPath), ":dictSha256Hash:")
            ],
        };

        var result = task.Execute();

        result.Should().BeTrue();
        var endpoints = StaticWebAssetEndpoint.FromItemGroup(task.UpdatedEndpoints);

        // Find the synthetic dcz endpoint (the one on the primary route serving dcz asset)
        var syntheticDcz = endpoints.FirstOrDefault(e =>
            e.Route == "candidate.js" &&
            e.AssetFile == Path.GetFullPath(dczPath));
        syntheticDcz.Should().NotBeNull("a synthetic dcz endpoint should be created at the primary route");

        // Should have both Content-Encoding and Available-Dictionary selectors
        syntheticDcz.Selectors.Should().Contain(s =>
            s.Name == "Content-Encoding" && s.Value == "dcz");
        syntheticDcz.Selectors.Should().Contain(s =>
            s.Name == "Available-Dictionary" && s.Value == ":dictSha256Hash:");

        // Should have Vary: Available-Dictionary header
        syntheticDcz.ResponseHeaders.Should().Contain(h =>
            h.Name == "Vary" && h.Value == "Available-Dictionary");
    }

    [Fact]
    public void DictionaryFormat_OriginalEndpointGetsUseDictionaryHeader()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var originalPath = Path.Combine("wwwroot", "candidate.js");
        var dczPath = Path.Combine("compressed", "candidate.js.dcz");

        var task = new ApplyCompressionNegotiation
        {
            BuildEngine = buildEngine.Object,
            CandidateAssets =
            [
                CreateCandidate(
                    originalPath,
                    "MyPackage",
                    "Discovered",
                    "candidate.js",
                    "All",
                    "All",
                    "original-fingerprint",
                    "original",
                    fileLength: 20
                ),
                CreateCandidate(
                    dczPath,
                    "MyPackage",
                    "Discovered",
                    "candidate.js",
                    "All",
                    "All",
                    "dcz-fingerprint",
                    "compressed",
                    Path.Combine("wwwroot", "candidate.js"),
                    "Content-Encoding",
                    "dcz",
                    5
                )
            ],
            CandidateEndpoints =
            [
                CreateCandidateEndpoint(
                    "candidate.js",
                    originalPath,
                    CreateHeaders("text/javascript", [("Content-Length", "20")])),
                CreateCandidateEndpoint(
                    "candidate.js.dcz",
                    dczPath,
                    CreateHeaders("text/javascript", [("Content-Length", "5")]))
            ],
            CompressionFormats = CreateCompressionFormats("dcz"),
            DictionaryCandidates =
            [
                CreateDictionaryCandidate(Path.GetFullPath(originalPath), ":dictSha256Hash:")
            ],
        };

        var result = task.Execute();

        result.Should().BeTrue();
        var endpoints = StaticWebAssetEndpoint.FromItemGroup(task.UpdatedEndpoints);

        // The original (primary) endpoint should have Use-As-Dictionary header
        var originalEndpoint = endpoints.FirstOrDefault(e =>
            e.Route == "candidate.js" &&
            e.AssetFile == Path.GetFullPath(originalPath));
        originalEndpoint.Should().NotBeNull("original endpoint should be in updated list");
        originalEndpoint.ResponseHeaders.Should().Contain(h =>
            h.Name == "Use-As-Dictionary" && h.Value == "match=\"/js/site.js\"");
        originalEndpoint.ResponseHeaders.Should().Contain(h =>
            h.Name == "Vary" && h.Value == "Available-Dictionary");
    }

    [Fact]
    public void DictionaryFormat_NonDictionaryFormatsUnchanged()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var originalPath = Path.Combine("wwwroot", "candidate.js");
        var gzPath = Path.Combine("compressed", "candidate.js.gz");
        var dczPath = Path.Combine("compressed", "candidate.js.dcz");

        var task = new ApplyCompressionNegotiation
        {
            BuildEngine = buildEngine.Object,
            CandidateAssets =
            [
                CreateCandidate(
                    originalPath,
                    "MyPackage",
                    "Discovered",
                    "candidate.js",
                    "All",
                    "All",
                    "original-fingerprint",
                    "original",
                    fileLength: 20
                ),
                CreateCandidate(
                    gzPath,
                    "MyPackage",
                    "Discovered",
                    "candidate.js",
                    "All",
                    "All",
                    "gz-fingerprint",
                    "compressed",
                    Path.Combine("wwwroot", "candidate.js"),
                    "Content-Encoding",
                    "gzip",
                    9
                ),
                CreateCandidate(
                    dczPath,
                    "MyPackage",
                    "Discovered",
                    "candidate.js",
                    "All",
                    "All",
                    "dcz-fingerprint",
                    "compressed",
                    Path.Combine("wwwroot", "candidate.js"),
                    "Content-Encoding",
                    "dcz",
                    5
                )
            ],
            CandidateEndpoints =
            [
                CreateCandidateEndpoint(
                    "candidate.js",
                    originalPath,
                    CreateHeaders("text/javascript", [("Content-Length", "20")])),
                CreateCandidateEndpoint(
                    "candidate.js.gz",
                    gzPath,
                    CreateHeaders("text/javascript", [("Content-Length", "9")])),
                CreateCandidateEndpoint(
                    "candidate.js.dcz",
                    dczPath,
                    CreateHeaders("text/javascript", [("Content-Length", "5")]))
            ],
            CompressionFormats = CreateCompressionFormats("gzip", "dcz"),
            DictionaryCandidates =
            [
                CreateDictionaryCandidate(Path.GetFullPath(originalPath), ":dictHash:")
            ],
        };

        var result = task.Execute();

        result.Should().BeTrue();
        var endpoints = StaticWebAssetEndpoint.FromItemGroup(task.UpdatedEndpoints);

        // The gzip synthetic endpoint should NOT have Available-Dictionary selector
        // (only dcz endpoints use that selector for content negotiation)
        var syntheticGz = endpoints.FirstOrDefault(e =>
            e.Route == "candidate.js" &&
            e.AssetFile == Path.GetFullPath(gzPath));
        syntheticGz.Should().NotBeNull();
        syntheticGz.Selectors.Should().Contain(s => s.Name == "Content-Encoding" && s.Value == "gzip");
        syntheticGz.Selectors.Should().NotContain(s => s.Name == "Available-Dictionary");

        // But gzip SHOULD have Use-As-Dictionary header and Vary: Available-Dictionary
        // Per RFC 9842, all content-negotiated responses for a resource should include
        // Use-As-Dictionary so the client can store the decompressed body as a dictionary.
        syntheticGz.ResponseHeaders.Should().Contain(h =>
            h.Name == "Use-As-Dictionary" && h.Value == "match=\"/js/site.js\"");
        syntheticGz.ResponseHeaders.Should().Contain(h =>
            h.Name == "Vary" && h.Value == "Available-Dictionary");
    }

    private static ITaskItem CreateDictionaryCandidate(string targetAssetIdentity, string hash, string matchPattern = null)
    {
        var dictionaryPath = Path.Combine("prev", "assets", "candidate.js");
        var item = new TaskItem(dictionaryPath);
        item.SetMetadata("Hash", hash);
        item.SetMetadata("TargetAsset", targetAssetIdentity);
        item.SetMetadata("MatchPattern", matchPattern ?? "js/site.js");
        return item;
    }
}
