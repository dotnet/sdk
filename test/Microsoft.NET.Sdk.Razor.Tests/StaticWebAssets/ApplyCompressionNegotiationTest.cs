// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
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
                    "original"
                ),
                CreateCandidate(
                    Path.Combine("compressed", "candidate.js.gz"),
                    "MyPackage",
                    "Discovered",
                    "candidate.js",
                    "All",
                    "All",
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
    public void AppliesContentNegotiationRules_ToAllEndpointsForExistingAssets()
    {
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var task = new ApplyCompressionNegotiation
        {
            BuildEngine = buildEngine.Object,
            CandidateAssets = new StaticWebAsset[]
            {
                new()
                {
                    Identity = """C:\work\sdk\artifacts\tmp\Debug\Build_Creates---20DEAECD\wwwroot\app.js""",
                    RelativePath = "app.js",
                    BasePath = "_content/ComponentApp",
                    AssetMode = "All",
                    AssetKind = "All",
                    AssetMergeSource = "",
                    SourceId = "ComponentApp",
                    CopyToOutputDirectory = "Never",
                    Fingerprint = "0tklt8fywl",
                    RelatedAsset = "",
                    ContentRoot = """C:\work\sdk\artifacts\tmp\Debug\Build_Creates---20DEAECD\wwwroot\""",
                    SourceType = "Discovered",
                    Integrity = "lClGOfcWqtQdAvO3zCRzZEg/4RmOMbr9/V54QO76j/A=",
                    AssetRole = "Primary",
                    AssetMergeBehavior = "",
                    AssetTraitValue = "",
                    AssetTraitName = "",
                    OriginalItemSpec = "wwwroot\app.js",
                    CopyToPublishDirectory = "PreserveNewest",
                },
                new()
                {
                    Identity = """C:\work\sdk\artifacts\tmp\Debug\Build_Creates---20DEAECD\obj\Debug\net9.0\scopedcss\bundle\ComponentApp.styles.css""",
                    RelativePath = "ComponentApp#[.{fingerprint}]?.styles.css",
                    BasePath = "_content/ComponentApp",
                    AssetMode = "CurrentProject",
                    AssetKind = "All",
                    AssetMergeSource = "",
                    SourceId = "ComponentApp",
                    CopyToOutputDirectory = "Never",
                    Fingerprint = "uwvzqsgimx",
                    RelatedAsset = "",
                    ContentRoot = """C:\work\sdk\artifacts\tmp\Debug\Build_Creates---20DEAECD\obj\Debug\net9.0\scopedcss\bundle\""",
                    SourceType = "Computed",
                    Integrity = "TlbZkCZHJzdAKFf3fWLJDNxplhFXy9gSrAReEBDlTvA=",
                    AssetRole = "Primary",
                    AssetMergeBehavior = "",
                    AssetTraitValue = "ApplicationBundle",
                    AssetTraitName = "ScopedCss",
                    OriginalItemSpec = """"C:\work\sdk\artifacts\tmp\Debug\Build_Creates---20DEAECD\obj\Debug\net9.0\scopedcss\bundle\ComponentApp.styles.css"""",
                    CopyToPublishDirectory = "PreserveNewest",
                },
                new()
                {
                    Identity = """C:\work\sdk\artifacts\tmp\Debug\Build_Creates---20DEAECD\obj\Debug\net9.0\scopedcss\projectbundle\ComponentApp.bundle.scp.css""",
                    RelativePath = "ComponentApp#[.{fingerprint}]!.bundle.scp.css",
                    BasePath = "_content/ComponentApp",
                    AssetMode = "Reference",
                    AssetKind = "All",
                    AssetMergeSource = "",
                    SourceId = "ComponentApp",
                    CopyToOutputDirectory = "Never",
                    Fingerprint = "uwvzqsgimx",
                    RelatedAsset = "",
                    ContentRoot = """C:\work\sdk\artifacts\tmp\Debug\Build_Creates---20DEAECD\obj\Debug\net9.0\scopedcss\projectbundle\""",
                    SourceType = "Computed",
                    Integrity = "TlbZkCZHJzdAKFf3fWLJDNxplhFXy9gSrAReEBDlTvA=",
                    AssetRole = "Primary",
                    AssetMergeBehavior = "",
                    AssetTraitValue = "ProjectBundle",
                    AssetTraitName = "ScopedCss",
                    OriginalItemSpec = """"C:\work\sdk\artifacts\tmp\Debug\Build_Creates---20DEAECD\obj\Debug\net9.0\scopedcss\projectbundle\ComponentApp.bundle.scp.css"""",
                    CopyToPublishDirectory = "PreserveNewest",
                },
                new()
                {
                    Identity = """C:\work\sdk\artifacts\tmp\Debug\Build_Creates---20DEAECD\obj\Debug\net9.0\compressed\3jkjor5twg.gz""",
                    RelativePath = "app.js.gz",
                    BasePath = "_content/ComponentApp",
                    AssetMode = "All",
                    AssetKind = "All",
                    AssetMergeSource = "",
                    SourceId = "ComponentApp",
                    CopyToOutputDirectory = "Never",
                    Fingerprint = "4494p56vyt",
                    RelatedAsset = """C:\work\sdk\artifacts\tmp\Debug\Build_Creates---20DEAECD\wwwroot\app.js""",
                    ContentRoot = """C:\work\sdk\artifacts\tmp\Debug\Build_Creates---20DEAECD\obj\Debug\net9.0\compressed\""",
                    SourceType = "Discovered",
                    Integrity = "JBRzZrf2Y9xv2+Odymyh9B4s0SZRVmPk+J1/hXatg84=",
                    AssetRole = "Alternative",
                    AssetMergeBehavior = "",
                    AssetTraitValue = "gzip",
                    AssetTraitName = "Content-Encoding",
                    OriginalItemSpec = """C:\work\sdk\artifacts\tmp\Debug\Build_Creates---20DEAECD\wwwroot\app.js""",
                    CopyToPublishDirectory = "PreserveNewest",
                },
                new()
                {
                    Identity = """C:\work\sdk\artifacts\tmp\Debug\Build_Creates---20DEAECD\obj\Debug\net9.0\compressed\18iuh44mu3.gz""",
                    RelativePath = "ComponentApp#[.{fingerprint}]?.styles.css.gz",
                    BasePath = "_content/ComponentApp",
                    AssetMode = "CurrentProject",
                    AssetKind = "All",
                    AssetMergeSource = "",
                    SourceId = "ComponentApp",
                    CopyToOutputDirectory = "Never",
                    Fingerprint = "2c5f17lky0",
                    RelatedAsset = """C:\work\sdk\artifacts\tmp\Debug\Build_Creates---20DEAECD\obj\Debug\net9.0\scopedcss\bundle\ComponentApp.styles.css""",
                    ContentRoot = """C:\work\sdk\artifacts\tmp\Debug\Build_Creates---20DEAECD\obj\Debug\net9.0\compressed\""",
                    SourceType = "Computed",
                    Integrity = "PsrlWTzC48XUSMKZ9t+HhgNHPzmN/sE0e7MCtaW0Ve8=",
                    AssetRole = "Alternative",
                    AssetMergeBehavior = "",
                    AssetTraitValue = "gzip",
                    AssetTraitName = "Content-Encoding",
                    OriginalItemSpec = """C:\work\sdk\artifacts\tmp\Debug\Build_Creates---20DEAECD\obj\Debug\net9.0\scopedcss\bundle\ComponentApp.styles.css""",
                    CopyToPublishDirectory = "PreserveNewest",
                },
                new()
                {
                    Identity = """C:\work\sdk\artifacts\tmp\Debug\Build_Creates---20DEAECD\obj\Debug\net9.0\compressed\3gzb92oy1i.gz""",
                    RelativePath = "ComponentApp#[.{fingerprint}]!.bundle.scp.css.gz",
                    BasePath = "_content/ComponentApp",
                    AssetMode = "Reference",
                    AssetKind = "All",
                    AssetMergeSource = "",
                    SourceId = "ComponentApp",
                    CopyToOutputDirectory = "Never",
                    Fingerprint = "2c5f17lky0",
                    RelatedAsset = """C:\work\sdk\artifacts\tmp\Debug\Build_Creates---20DEAECD\obj\Debug\net9.0\scopedcss\projectbundle\ComponentApp.bundle.scp.css""",
                    ContentRoot = """C:\work\sdk\artifacts\tmp\Debug\Build_Creates---20DEAECD\obj\Debug\net9.0\compressed\""",
                    SourceType = "Computed",
                    Integrity = "PsrlWTzC48XUSMKZ9t+HhgNHPzmN/sE0e7MCtaW0Ve8=",
                    AssetRole = "Alternative",
                    AssetMergeBehavior = "",
                    AssetTraitValue = "gzip",
                    AssetTraitName = "Content-Encoding",
                    OriginalItemSpec = """C:\work\sdk\artifacts\tmp\Debug\Build_Creates---20DEAECD\obj\Debug\net9.0\scopedcss\projectbundle\ComponentApp.bundle.scp.css""",
                    CopyToPublishDirectory = "PreserveNewest",
                }
            }.Select(e => e.ToTaskItem()).ToArray(),
            CandidateEndpoints =new StaticWebAssetEndpoint[]
            {
                new()
                {
                        Route = "app.js",
                        AssetFile = """C:\work\sdk\artifacts\tmp\Debug\Build_Creates---20DEAECD\wwwroot\app.js""",
                        Selectors = [],
                        ResponseHeaders = [new() { Name = "Accept-Ranges", Value = "bytes"},new() { Name = "Content-Length", Value = "28"},new() { Name = "Content-Type", Value = "text/javascript"},new() { Name = "ETag", Value = "\u0022lClGOfcWqtQdAvO3zCRzZEg/4RmOMbr9/V54QO76j/A=\u0022"},new() { Name = "Last-Modified", Value = "Tue, 21 May 2024 16:23:41 GMT"}],
                        EndpointProperties = [],
                },
                new()
                {
                        Route = "app.js.gz",
                        AssetFile = """C:\work\sdk\artifacts\tmp\Debug\Build_Creates---20DEAECD\obj\Debug\net9.0\compressed\3jkjor5twg.gz""",
                        Selectors = [],
                        ResponseHeaders = [new() { Name = "Accept-Ranges", Value = "bytes"},new() { Name = "Content-Length", Value = "48"},new() { Name = "Content-Type", Value = "text/javascript"},new() { Name = "ETag", Value = "\u0022JBRzZrf2Y9xv2\u002BOdymyh9B4s0SZRVmPk\u002BJ1/hXatg84=\u0022"},new() { Name = "Last-Modified", Value = "Tue, 21 May 2024 16:23:41 GMT"}],
                        EndpointProperties = [],
                },
                new()
                {
                        Route = "ComponentApp.2c5f17lky0.bundle.scp.css.gz",
                        AssetFile = """C:\work\sdk\artifacts\tmp\Debug\Build_Creates---20DEAECD\obj\Debug\net9.0\compressed\3gzb92oy1i.gz""",
                        Selectors = [],
                        ResponseHeaders = [new() { Name = "Accept-Ranges", Value = "bytes"},new() { Name = "Content-Length", Value = "168"},new() { Name = "Content-Type", Value = "text/css"},new() { Name = "ETag", Value = "\u0022PsrlWTzC48XUSMKZ9t\u002BHhgNHPzmN/sE0e7MCtaW0Ve8=\u0022"},new() { Name = "Last-Modified", Value = "Tue, 21 May 2024 16:23:50 GMT"},new() { Name = "Cache-Control", Value = "max-age=604800, immutable"}],
                        EndpointProperties = [new() { Name = "fingerprint", Value = "2c5f17lky0"}]
                },
                new()
                {
                        Route = "ComponentApp.2c5f17lky0.styles.css.gz",
                        AssetFile = """C:\work\sdk\artifacts\tmp\Debug\Build_Creates---20DEAECD\obj\Debug\net9.0\compressed\18iuh44mu3.gz""",
                        Selectors = [],
                        ResponseHeaders = [new() { Name = "Accept-Ranges", Value = "bytes"},new() { Name = "Content-Length", Value = "168"},new() { Name = "Content-Type", Value = "text/css"},new() { Name = "ETag", Value = "\u0022PsrlWTzC48XUSMKZ9t\u002BHhgNHPzmN/sE0e7MCtaW0Ve8=\u0022"},new() { Name = "Last-Modified", Value = "Tue, 21 May 2024 16:23:50 GMT"},new() { Name = "Cache-Control", Value = "max-age=604800, immutable"}],
                        EndpointProperties = [new() { Name = "fingerprint", Value = "2c5f17lky0"}]
                },
                new()
                {
                        Route = "ComponentApp.bundle.scp.css",
                        AssetFile = """C:\work\sdk"\artifacts\tmp\Debug\Build_Creates---20DEAECD\obj\Debug\net9.0\scopedcss\projectbundle\ComponentApp.bundle.scp.css""",
                        Selectors = [],
                        ResponseHeaders = [new() { Name = "Accept-Ranges", Value = "bytes"},new() { Name = "Content-Length", Value = "234"},new() { Name = "Content-Type", Value = "text/css"},new() { Name = "ETag", Value = "\u0022TlbZkCZHJzdAKFf3fWLJDNxplhFXy9gSrAReEBDlTvA=\u0022"},new() { Name = "Last-Modified", Value = "Tue, 21 May 2024 16:23:50 GMT"}],
                        EndpointProperties = [],
                },
                new()
                {
                        Route = "ComponentApp.bundle.scp.css.gz",
                        AssetFile = """C:\work\sdk\artifacts"\tmp\Debug\Build_Creates---20DEAECD\obj\Debug\net9.0\compressed\3gzb92oy1i.gz""",
                        Selectors = [],
                        ResponseHeaders = [new() { Name = "Accept-Ranges", Value = "bytes"},new() { Name = "Content-Length", Value = "168"},new() { Name = "Content-Type", Value = "text/css"},new() { Name = "ETag", Value = "\u0022PsrlWTzC48XUSMKZ9t\u002BHhgNHPzmN/sE0e7MCtaW0Ve8=\u0022"},new() { Name = "Last-Modified", Value = "Tue, 21 May 2024 16:23:50 GMT"}],
                        EndpointProperties = [],
                },
                new()
                {
                        Route = "ComponentApp.styles.css",
                        AssetFile = """C:\work"\sdk\artifacts\tmp\Debug\Build_Creates---20DEAECD\obj\Debug\net9.0\scopedcss\bundle\ComponentApp.styles.css""",
                        Selectors = [],
                        ResponseHeaders = [new() { Name = "Accept-Ranges", Value = "bytes"},new() { Name = "Content-Length", Value = "234"},new() { Name = "Content-Type", Value = "text/css"},new() { Name = "ETag", Value = "\u0022TlbZkCZHJzdAKFf3fWLJDNxplhFXy9gSrAReEBDlTvA=\u0022"},new() { Name = "Last-Modified", Value = "Tue, 21 May 2024 16:23:50 GMT"}],
                        EndpointProperties = [],
                },
                new()
                {
                        Route = "ComponentApp.styles.css.gz",
                        AssetFile = """C:\work\sdk"\artifacts\tmp\Debug\Build_Creates---20DEAECD\obj\Debug\net9.0\compressed\18iuh44mu3.gz""",
                        Selectors = [],
                        ResponseHeaders = [new() { Name = "Accept-Ranges", Value = "bytes"},new() { Name = "Content-Length", Value = "168"},new() { Name = "Content-Type", Value = "text/css"},new() { Name = "ETag", Value = "\u0022PsrlWTzC48XUSMKZ9t\u002BHhgNHPzmN/sE0e7MCtaW0Ve8=\u0022"},new() { Name = "Last-Modified", Value = "Tue, 21 May 2024 16:23:50 GMT"}],
                        EndpointProperties = [],
                },
                new()
                {
                        Route = "ComponentApp.uwvzqsgimx.bundle.scp.css",
                        AssetFile = """C:\work\sdk\artifacts\tmp\Debug\Build_Creates---20DEAECD\obj\Debug\net9.0\scopedcss\projectbundle\ComponentApp.bundle.scp.css""",
                        Selectors = [],
                        ResponseHeaders = [new() { Name = "Accept-Ranges", Value = "bytes"},new() { Name = "Content-Length", Value = "234"},new() { Name = "Content-Type", Value = "text/css"},new() { Name = "ETag", Value = "\u0022TlbZkCZHJzdAKFf3fWLJDNxplhFXy9gSrAReEBDlTvA=\u0022"},new() { Name = "Last-Modified", Value = "Tue, 21 May 2024 16:23:50 GMT"},new() { Name = "Cache-Control", Value = "max-age=604800, immutable"}],
                        EndpointProperties = [new() { Name = "fingerprint", Value = "uwvzqsgimx"}]
                },
                new()
                {
                        Route = "ComponentApp.uwvzqsgimx.styles.css",
                        AssetFile = """C:\work\sdk\artifacts\tmp\Debug\Build_Creates---20DEAECD\obj\Debug\net9.0\scopedcss\bundle\ComponentApp.styles.css""",
                        Selectors = [],
                        ResponseHeaders = [new() { Name = "Accept-Ranges", Value = "bytes"},new() { Name = "Content-Length", Value = "234"},new() { Name = "Content-Type", Value = "text/css"},new() { Name = "ETag", Value = "\u0022TlbZkCZHJzdAKFf3fWLJDNxplhFXy9gSrAReEBDlTvA=\u0022"},new() { Name = "Last-Modified", Value = "Tue, 21 May 2024 16:23:50 GMT"},new() { Name = "Cache-Control", Value = "max-age=604800, immutable"}],
                        EndpointProperties = [new() { Name = "fingerprint", Value = "uwvzqsgimx"}]
                }
            }.Select(e => e.ToTaskItem()).ToArray(),
            TestResolveFileLength = fn => 10,
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
                    "original"
                ),
                CreateCandidate(
                    Path.Combine("compressed", "candidate.js.gz"),
                    "MyPackage",
                    "Discovered",
                    "candidate.js",
                    "All",
                    "All",
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
                    "original"
                ),
                CreateCandidate(
                    Path.Combine("compressed", "candidate.js.gz"),
                    "MyPackage",
                    "Discovered",
                    "candidate.js",
                    "All",
                    "All",
                    "compressed",
                    Path.Combine("wwwroot", "candidate.js"),
                    "Content-Encoding",
                    "gzip"
                )
            ],
            CandidateEndpoints = new StaticWebAssetEndpoint[]
            {
                new() {
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
                new() {
                    Route = "candidate.js",
                    AssetFile = Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")),
                    ResponseHeaders =
                    [
                        new (){ Name = "Content-Type", Value = "text/javascript" }
                    ],
                    EndpointProperties = [],
                    Selectors = [],
                },
                new() {
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
                new() {
                    Route = "candidate.fingerprint.js",
                    AssetFile = Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")),
                    ResponseHeaders =
                    [
                        new () { Name = "Content-Type", Value = "text/javascript" }
                    ],
                    EndpointProperties = [],
                    Selectors = [],
                },
                new() {
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
                    "original"
                ),
                CreateCandidate(
                    Path.Combine("compressed", "candidate.js.gz"),
                    "MyPackage",
                    "Discovered",
                    "candidate.js",
                    "All",
                    "All",
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
                    "compressed",
                    Path.Combine("wwwroot", "candidate.js"),
                    "Content-Encoding",
                    "br"
                )
            ],
            CandidateEndpoints = new StaticWebAssetEndpoint[]
            {
                new() {
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
                new() {
                    Route = "candidate.js",
                    AssetFile = Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")),
                    ResponseHeaders =
                    [
                        new (){ Name = "Content-Type", Value = "text/javascript" }
                    ],
                    EndpointProperties = [],
                    Selectors = [],
                },
                new() {
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
                new() {
                    Route = "candidate.fingerprint.js",
                    AssetFile = Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")),
                    ResponseHeaders =
                    [
                        new () { Name = "Content-Type", Value = "text/javascript" }
                    ],
                    EndpointProperties = [],
                    Selectors = [],
                },
                new() {
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
                new() {
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
