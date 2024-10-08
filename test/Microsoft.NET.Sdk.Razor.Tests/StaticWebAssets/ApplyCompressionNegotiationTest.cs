// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Sdk.StaticWebAssets.Tasks;
using Moq;

namespace Microsoft.NET.Sdk.Razor.Tests.StaticWebAssets;

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
            TestResolveFileLength = value => value switch
            {
                string candidateGz when candidateGz.EndsWith(Path.Combine("compressed", "candidate.js.gz")) => 9,
                string candidate when candidate.EndsWith(Path.Combine("compressed", "candidate.js")) => 20,
                _ => throw new InvalidOperationException()
            }
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
                    new () { Name = "Vary", Value = "Content-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = [ new () { Name = "Content-Encoding", Value = "gzip", Quality = "0.100000000000" } ],
            },
            new ()
            {
                Route = "candidate.js",
                AssetFile = Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Length", Value = "20" },
                    new () { Name = "Content-Type", Value = "text/javascript" },
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
                    new () { Name = "Vary", Value = "Content-Encoding" }
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
                Path.Combine("wwwroot", "candidate.js"),
                "MyPackage",
                "Discovered",
                "candidate#[.{fingerprint}]?.js",
                "All",
                "All",
                "original-fingerprint",
                "original"
            )
        ];

        var compressedTask = new ResolveCompressedAssets
        {
            BuildEngine = buildEngine.Object,
            CandidateAssets = [.. candidateAssets],
            Formats = "gzip;brotli",
            IncludePatterns = "*.js",
            OutputPath = AppContext.BaseDirectory
        };
        compressedTask.Execute().Should().BeTrue();

        var compressedAssets = compressedTask.AssetsToCompress;
        compressedAssets[0].SetMetadata(nameof(StaticWebAsset.Fingerprint), "gzip");
        compressedAssets[0].SetMetadata(nameof(StaticWebAsset.Integrity), "compressed-gzip");
        compressedAssets[1].SetMetadata(nameof(StaticWebAsset.Fingerprint), "brotli");
        compressedAssets[1].SetMetadata(nameof(StaticWebAsset.Integrity), "compressed-brotli");
        candidateAssets.AddRange(compressedAssets);
        var expectedName = Path.GetFileNameWithoutExtension(compressedAssets[0].ItemSpec);
        var defineStaticAssetEndpointsTask = new DefineStaticWebAssetEndpoints
        {
            TestLengthResolver = value => value switch
            {
                string candidateBr when candidateBr.EndsWith(".br") => 7,
                string candidateGz when candidateGz.EndsWith(".gz") => 9,
                string candidate when candidate.EndsWith(".js") => 20,
                _ => throw new InvalidOperationException()
            },
            TestLastWriteResolver = value => now,
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
            TestResolveFileLength = value => value switch
            {
                string candidateBr when candidateBr.EndsWith(".br") => 7,
                string candidateGz when candidateGz.EndsWith(".gz") => 9,
                string candidate when candidate.EndsWith(".js") => 20,
                _ => throw new InvalidOperationException()
            }
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
                            Quality = "0.125000000000"
                    }
                ],
            ResponseHeaders = [
            new ()
            {
                Name = "Accept-Ranges",
                Value = "bytes"
            },
            new ()
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
                Name = "ETag",
                Value = "W/\u0022original\u0022"
            },
            new ()
            {
                Name = "Last-Modified",
                Value = now.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture)
            },
            new ()
            {
                Name = "Vary",
                Value = "Content-Encoding"
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
                Quality = "0.100000000000"
                }
            ],
            ResponseHeaders = [
                new ()
                {
                    Name = "Accept-Ranges",
                    Value = "bytes"
                },
                new ()
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
                    Name = "ETag",
                    Value = "W/\u0022original\u0022"
                },
                new ()
                {
                    Name = "Last-Modified",
                    Value = now.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture)
                },
                new ()
                {
                    Name = "Vary",
                    Value = "Content-Encoding"
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
                ResponseHeaders = [
                new ()
                {
                    Name = "Accept-Ranges",
                    Value = "bytes"
                },
                new ()
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
                ResponseHeaders = [
                new ()
                {
                        Name = "Accept-Ranges",
                        Value = "bytes"
                },
                new ()
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
                    Value = "Content-Encoding"
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
                ResponseHeaders = [
                new ()
                {
                    Name = "Accept-Ranges",
                    Value = "bytes"
                },
                new ()
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
                    Value = "Content-Encoding"
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
                Quality = "0.125000000000"
                }
            ],
            ResponseHeaders = [
                new ()
                {
                    Name = "Accept-Ranges",
                    Value = "bytes"
                },
                new ()
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
                    Name = "ETag",
                    Value = "W/\u0022original\u0022"
                },
                new ()
                {
                    Name = "Last-Modified",
                    Value = now.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture)
                },
                new ()
                {
                    Name = "Vary",
                    Value = "Content-Encoding"
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
                Quality = "0.100000000000"
                }
            ],
            ResponseHeaders = [
                new ()
                {
                    Name = "Accept-Ranges",
                    Value = "bytes"
                },
                new ()
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
                    Name = "ETag",
                    Value = "W/\u0022original\u0022"
                },
                new ()
                {
                    Name = "Last-Modified",
                    Value = now.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture)
                },
                new ()
                {
                    Name = "Vary",
                    Value = "Content-Encoding"
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
                ResponseHeaders = [
                new ()
                {
                    Name = "Accept-Ranges",
                    Value = "bytes"
                },
                new ()
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
                ResponseHeaders = [
                new ()
                {
                    Name = "Accept-Ranges",
                    Value = "bytes"
                },
                new ()
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
                    Value = "Content-Encoding"
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
                ResponseHeaders = [
                new () {
                Name = "Accept-Ranges",
                    Value = "bytes"
                },
                new () {
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
                    Value = "Content-Encoding"
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
            TestResolveFileLength = value => value switch
            {
                string candidateGz when candidateGz.EndsWith(Path.Combine("compressed", "candidate.js.gz")) => 9,
                string candidate when candidate.EndsWith(Path.Combine("compressed", "candidate.js")) => 20,
                _ => throw new InvalidOperationException()
            }
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
                    new () { Name = "Vary", Value = "Content-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = [ new () { Name = "Content-Encoding", Value = "gzip", Quality = "0.100000000000" } ],
            },
            new ()
            {
                Route = "candidate.js",
                AssetFile = Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Type", Value = "text/javascript" }
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
                    new () { Name = "Vary", Value = "Content-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = [ new () { Name = "Content-Encoding", Value = "gzip", Quality = "0.100000000000" } ],
            },
            new ()
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
            new ()
            {
                Route = "candidate.js.gz",
                AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.gz")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Encoding", Value = "gzip" },
                    new () { Name = "Content-Type", Value = "text/javascript" },
                    new () { Name = "Vary", Value = "Content-Encoding" }
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
                        new (){ Name = "Vary", Value = "Content-Encoding" }
                    ],
                    EndpointProperties = [],
                    Selectors = [ new StaticWebAssetEndpointSelector { Name = "Content-Encoding", Value = "gzip", Quality = "0.100000000000" } ],
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
                        new (){ Name = "Vary", Value = "Content-Encoding" }
                    ],
                    EndpointProperties = [],
                    Selectors = [ new () { Name = "Content-Encoding", Value = "gzip", Quality = "0.100000000000" } ],
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
                        new () { Name = "Vary", Value = "Content-Encoding" }
                    ],
                    EndpointProperties = [],
                    Selectors = []
                }
            }.Select(e => e.ToTaskItem()).ToArray(),
            TestResolveFileLength = value => value switch
            {
                string candidateGz when candidateGz.EndsWith(Path.Combine("compressed", "candidate.js.gz")) => 9,
                string candidate when candidate.EndsWith(Path.Combine("compressed", "candidate.js")) => 20,
                _ => throw new InvalidOperationException()
            }
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
                    new () { Name = "Vary", Value = "Content-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = [ new StaticWebAssetEndpointSelector { Name = "Content-Encoding", Value = "gzip", Quality = "0.100000000000" } ],
            },
            new StaticWebAssetEndpoint
            {
                Route = "candidate.js",
                AssetFile = Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Type", Value = "text/javascript" }
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
                    new () { Name = "Vary", Value = "Content-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = [ new StaticWebAssetEndpointSelector { Name = "Content-Encoding", Value = "gzip", Quality = "0.100000000000" } ],
            },
            new StaticWebAssetEndpoint
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
            new StaticWebAssetEndpoint
            {
                Route = "candidate.js.gz",
                AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.gz")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Encoding", Value = "gzip" },
                    new () { Name = "Content-Type", Value = "text/javascript" },
                    new () { Name = "Vary", Value = "Content-Encoding" }
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
                    "original"
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
                    "gzip"
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
                    "br"
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
                        new (){ Name = "Vary", Value = "Content-Encoding" }
                    ],
                    EndpointProperties = [],
                    Selectors = [ new StaticWebAssetEndpointSelector { Name = "Content-Encoding", Value = "gzip", Quality = "0.100000000000" } ],
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
                        new (){ Name = "Vary", Value = "Content-Encoding" }
                    ],
                    EndpointProperties = [],
                    Selectors = [ new () { Name = "Content-Encoding", Value = "gzip", Quality = "0.100000000000" } ],
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
                        new () { Name = "Vary", Value = "Content-Encoding" }
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
            TestResolveFileLength = value => value switch
            {
                string candidateGz when candidateGz.EndsWith(Path.Combine("compressed", "candidate.js.gz")) => 9,
                string candidateGz when candidateGz.EndsWith(Path.Combine("compressed", "candidate.js.br")) => 9,
                string candidate when candidate.EndsWith(Path.Combine("compressed", "candidate.js")) => 20,
                _ => throw new InvalidOperationException()
            }
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
                    new () { Name = "Vary", Value = "Content-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = [ new StaticWebAssetEndpointSelector { Name = "Content-Encoding", Value = "gzip", Quality = "0.100000000000" } ],
            },
            new StaticWebAssetEndpoint
            {
                Route = "candidate.js",
                AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.br")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Encoding", Value = "br" },
                    new () { Name = "Content-Type", Value = "text/javascript" },
                    new () { Name = "Vary", Value = "Content-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = [ new StaticWebAssetEndpointSelector { Name = "Content-Encoding", Value = "br", Quality = "0.100000000000" } ],
            },
            new StaticWebAssetEndpoint
            {
                Route = "candidate.js",
                AssetFile = Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Type", Value = "text/javascript" }
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
                    new () { Name = "Vary", Value = "Content-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = [ new StaticWebAssetEndpointSelector { Name = "Content-Encoding", Value = "gzip", Quality = "0.100000000000" } ],
            },
            new StaticWebAssetEndpoint
            {
                Route = "candidate.fingerprint.js",
                AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.br")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Encoding", Value = "br" },
                    new () { Name = "Content-Type", Value = "text/javascript" },
                    new () { Name = "Vary", Value = "Content-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = [ new StaticWebAssetEndpointSelector { Name = "Content-Encoding", Value = "br", Quality = "0.100000000000" } ],
            },
            new StaticWebAssetEndpoint
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
            new StaticWebAssetEndpoint
            {
                Route = "candidate.js.gz",
                AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.gz")),
                ResponseHeaders =
                [
                    new () { Name = "Content-Encoding", Value = "gzip" },
                    new () { Name = "Content-Type", Value = "text/javascript" },
                    new () { Name = "Vary", Value = "Content-Encoding" }
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
                    new () { Name = "Vary", Value = "Content-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = []
            }
        ]);
    }

    private StaticWebAssetEndpointSelector[] CreateContentEcondingSelector(string name, string value)
    {
        return
        [
            new StaticWebAssetEndpointSelector
            {
                Name = name,
                    Value = value,
                Quality = "0.100000000000"
            }
        ];
    }

    private StaticWebAssetEndpointResponseHeader[] CreateHeaders(string contentType, params (string name, string value)[] AdditionalHeaders)
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

    private ITaskItem CreateCandidate(
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
        string assetTraitValue = "")
    {
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
        };

        result.ApplyDefaults();
        result.Normalize();

        return result.ToTaskItem();
    }

    private ITaskItem CreateCandidateEndpoint(
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
}
