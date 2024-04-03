// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
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
        endpoints.Should().BeEquivalentTo([
            new StaticWebAssetEndpoint
            {
                Route = "candidate.js",
                AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.gz")),
                ResponseHeaders =
                [
                    new StaticWebAssetEndpointResponseHeader { Name = "Content-Encoding", Value = "gzip" },
                    new StaticWebAssetEndpointResponseHeader { Name = "Content-Type", Value = "text/javascript" },
                    new StaticWebAssetEndpointResponseHeader { Name = "Vary", Value = "Content-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = [ new StaticWebAssetEndpointSelector { Name = "Content-Encoding", Value = "gzip", Quality = "0.100000" } ],
            },
            new StaticWebAssetEndpoint
            {
                Route = "candidate.js",
                AssetFile = Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")),
                ResponseHeaders =
                [
                    new StaticWebAssetEndpointResponseHeader { Name = "Content-Type", Value = "text/javascript" }
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
                    new StaticWebAssetEndpointResponseHeader { Name = "Content-Encoding", Value = "gzip" },
                    new StaticWebAssetEndpointResponseHeader { Name = "Content-Type", Value = "text/javascript" },
                    new StaticWebAssetEndpointResponseHeader { Name = "Vary", Value = "Content-Encoding" }
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
        endpoints.Should().BeEquivalentTo([
            new StaticWebAssetEndpoint
            {
                Route = "candidate.js",
                AssetFile = Path.GetFullPath(Path.Combine("compressed", "candidate.js.gz")),
                ResponseHeaders =
                [
                    new StaticWebAssetEndpointResponseHeader { Name = "Content-Encoding", Value = "gzip" },
                    new StaticWebAssetEndpointResponseHeader { Name = "Content-Type", Value = "text/javascript" },
                    new StaticWebAssetEndpointResponseHeader { Name = "Vary", Value = "Content-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = [ new StaticWebAssetEndpointSelector { Name = "Content-Encoding", Value = "gzip", Quality = "0.100000" } ],
            },
            new StaticWebAssetEndpoint
            {
                Route = "candidate.js",
                AssetFile = Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")),
                ResponseHeaders =
                [
                    new StaticWebAssetEndpointResponseHeader { Name = "Content-Type", Value = "text/javascript" }
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
                    new StaticWebAssetEndpointResponseHeader { Name = "Content-Encoding", Value = "gzip" },
                    new StaticWebAssetEndpointResponseHeader { Name = "Content-Type", Value = "text/javascript" },
                    new StaticWebAssetEndpointResponseHeader { Name = "Vary", Value = "Content-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = [ new StaticWebAssetEndpointSelector { Name = "Content-Encoding", Value = "gzip", Quality = "0.100000" } ],
            },
            new StaticWebAssetEndpoint
            {
                Route = "candidate.fingerprint.js",
                AssetFile = Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")),
                ResponseHeaders =
                [
                    new StaticWebAssetEndpointResponseHeader { Name = "Content-Type", Value = "text/javascript" }
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
                    new StaticWebAssetEndpointResponseHeader { Name = "Content-Encoding", Value = "gzip" },
                    new StaticWebAssetEndpointResponseHeader { Name = "Content-Type", Value = "text/javascript" },
                    new StaticWebAssetEndpointResponseHeader { Name = "Vary", Value = "Content-Encoding" }
                ],
                EndpointProperties = [],
                Selectors = []
            }
        ]);
    }

    [Fact]
    public void EncodingNegotiation_Integration()
    {
        var assetCandidatesJson = """
            [
                {
                    "Identity": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\compressed\\8j96a56wcg.gz",
                    "RelativePath": "ComponentApp.bundle.scp.css.gz",
                    "BasePath": "_content/ComponentApp",
                    "AssetMode": "Reference",
                    "AssetKind": "All",
                    "AssetMergeSource": "",
                    "SourceId": "ComponentApp",
                    "CopyToOutputDirectory": "Never",
                    "Fingerprint": "2c5f17lky0",
                    "RelatedAsset": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\scopedcss\\projectbundle\\ComponentApp.bundle.scp.css",
                    "ContentRoot": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\compressed\\",
                    "SourceType": "Computed",
                    "Integrity": "PsrlWTzC48XUSMKZ9t+HhgNHPzmN/sE0e7MCtaW0Ve8=",
                    "AssetRole": "Alternative",
                    "AssetMergeBehavior": "",
                    "AssetTraitValue": "gzip",
                    "AssetTraitName": "Content-Encoding",
                    "OriginalItemSpec": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\scopedcss\\projectbundle\\ComponentApp.bundle.scp.css",
                    "CopyToPublishDirectory": "PreserveNewest"
                },
                {
                    "Identity": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\compressed\\k3svc14xi8.gz",
                    "RelativePath": "ComponentApp.styles.css.gz",
                    "BasePath": "_content/ComponentApp",
                    "AssetMode": "CurrentProject",
                    "AssetKind": "All",
                    "AssetMergeSource": "",
                    "SourceId": "ComponentApp",
                    "CopyToOutputDirectory": "Never",
                    "Fingerprint": "2c5f17lky0",
                    "RelatedAsset": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\scopedcss\\bundle\\ComponentApp.styles.css",
                    "ContentRoot": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\compressed\\",
                    "SourceType": "Computed",
                    "Integrity": "PsrlWTzC48XUSMKZ9t+HhgNHPzmN/sE0e7MCtaW0Ve8=",
                    "AssetRole": "Alternative",
                    "AssetMergeBehavior": "",
                    "AssetTraitValue": "gzip",
                    "AssetTraitName": "Content-Encoding",
                    "OriginalItemSpec": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\scopedcss\\bundle\\ComponentApp.styles.css",
                    "CopyToPublishDirectory": "PreserveNewest"
                },
                {
                    "Identity": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\scopedcss\\bundle\\ComponentApp.styles.css",
                    "RelativePath": "ComponentApp.styles.css",
                    "BasePath": "_content/ComponentApp",
                    "AssetMode": "CurrentProject",
                    "AssetKind": "All",
                    "AssetMergeSource": "",
                    "SourceId": "ComponentApp",
                    "CopyToOutputDirectory": "Never",
                    "Fingerprint": "uwvzqsgimx",
                    "RelatedAsset": "",
                    "ContentRoot": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\scopedcss\\bundle\\",
                    "SourceType": "Computed",
                    "Integrity": "TlbZkCZHJzdAKFf3fWLJDNxplhFXy9gSrAReEBDlTvA=",
                    "AssetRole": "Primary",
                    "AssetMergeBehavior": "",
                    "AssetTraitValue": "ApplicationBundle",
                    "AssetTraitName": "ScopedCss",
                    "OriginalItemSpec": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\scopedcss\\bundle\\ComponentApp.styles.css",
                    "CopyToPublishDirectory": "PreserveNewest"
                },
                {
                    "Identity": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\scopedcss\\projectbundle\\ComponentApp.bundle.scp.css",
                    "RelativePath": "ComponentApp.bundle.scp.css",
                    "BasePath": "_content/ComponentApp",
                    "AssetMode": "Reference",
                    "AssetKind": "All",
                    "AssetMergeSource": "",
                    "SourceId": "ComponentApp",
                    "CopyToOutputDirectory": "Never",
                    "Fingerprint": "uwvzqsgimx",
                    "RelatedAsset": "",
                    "ContentRoot": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\scopedcss\\projectbundle\\",
                    "SourceType": "Computed",
                    "Integrity": "TlbZkCZHJzdAKFf3fWLJDNxplhFXy9gSrAReEBDlTvA=",
                    "AssetRole": "Primary",
                    "AssetMergeBehavior": "",
                    "AssetTraitValue": "ProjectBundle",
                    "AssetTraitName": "ScopedCss",
                    "OriginalItemSpec": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\scopedcss\\projectbundle\\ComponentApp.bundle.scp.css",
                    "CopyToPublishDirectory": "PreserveNewest"
                },
                {
                    "Identity": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\compressed\\publish\\k3svc14xi8.br",
                    "RelativePath": "ComponentApp.styles.css.br",
                    "BasePath": "_content/ComponentApp",
                    "AssetMode": "CurrentProject",
                    "AssetKind": "All",
                    "AssetMergeSource": "",
                    "SourceId": "ComponentApp",
                    "CopyToOutputDirectory": "Never",
                    "Fingerprint": "95f2865ye4",
                    "RelatedAsset": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\scopedcss\\bundle\\ComponentApp.styles.css",
                    "ContentRoot": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\compressed\\publish\\",
                    "SourceType": "Computed",
                    "Integrity": "LWnMpOyUHO4LpFOxNrhon9FCyblI5rQaMgyJPfjvZtg=",
                    "AssetRole": "Alternative",
                    "AssetMergeBehavior": "",
                    "AssetTraitValue": "br",
                    "AssetTraitName": "Content-Encoding",
                    "OriginalItemSpec": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\scopedcss\\bundle\\ComponentApp.styles.css",
                    "CopyToPublishDirectory": "PreserveNewest"
                },
                {
                    "Identity": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\compressed\\publish\\8j96a56wcg.br",
                    "RelativePath": "ComponentApp.bundle.scp.css.br",
                    "BasePath": "_content/ComponentApp",
                    "AssetMode": "Reference",
                    "AssetKind": "All",
                    "AssetMergeSource": "",
                    "SourceId": "ComponentApp",
                    "CopyToOutputDirectory": "Never",
                    "Fingerprint": "95f2865ye4",
                    "RelatedAsset": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\scopedcss\\projectbundle\\ComponentApp.bundle.scp.css",
                    "ContentRoot": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\compressed\\publish\\",
                    "SourceType": "Computed",
                    "Integrity": "LWnMpOyUHO4LpFOxNrhon9FCyblI5rQaMgyJPfjvZtg=",
                    "AssetRole": "Alternative",
                    "AssetMergeBehavior": "",
                    "AssetTraitValue": "br",
                    "AssetTraitName": "Content-Encoding",
                    "OriginalItemSpec": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\scopedcss\\projectbundle\\ComponentApp.bundle.scp.css",
                    "CopyToPublishDirectory": "PreserveNewest"
                },
                {
                    "Identity": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\compressed\\vdjfplf5i2.gz",
                    "RelativePath": "app.js.gz",
                    "BasePath": "_content/ComponentApp",
                    "AssetMode": "All",
                    "AssetKind": "Publish",
                    "AssetMergeSource": "",
                    "SourceId": "ComponentApp",
                    "CopyToOutputDirectory": "Never",
                    "Fingerprint": "u9d8oeh4c0",
                    "RelatedAsset": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\wwwroot\\app.publish.js",
                    "ContentRoot": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\compressed\\",
                    "SourceType": "Discovered",
                    "Integrity": "MhWnj64DNaFcwJt2dnZR14OYzMypKWOopajvF9gHZoE=",
                    "AssetRole": "Alternative",
                    "AssetMergeBehavior": "",
                    "AssetTraitValue": "gzip",
                    "AssetTraitName": "Content-Encoding",
                    "OriginalItemSpec": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\wwwroot\\app.publish.js",
                    "CopyToPublishDirectory": "PreserveNewest"
                },
                {
                    "Identity": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\wwwroot\\app.publish.js",
                    "RelativePath": "app.js",
                    "BasePath": "_content/ComponentApp",
                    "AssetMode": "All",
                    "AssetKind": "Publish",
                    "AssetMergeSource": "",
                    "SourceId": "ComponentApp",
                    "CopyToOutputDirectory": "Never",
                    "Fingerprint": "r1ue4mnokm",
                    "RelatedAsset": "",
                    "ContentRoot": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\wwwroot\\",
                    "SourceType": "Discovered",
                    "Integrity": "YWTY9gwbxmffjp756H8XAAEQ5BoRO1ujOAs+icruBEI=",
                    "AssetRole": "Primary",
                    "AssetMergeBehavior": "",
                    "AssetTraitValue": "",
                    "AssetTraitName": "",
                    "OriginalItemSpec": "wwwroot\\app.publish.js",
                    "CopyToPublishDirectory": "PreserveNewest"
                },
                {
                    "Identity": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\compressed\\publish\\vdjfplf5i2.br",
                    "RelativePath": "app.js.br",
                    "BasePath": "_content/ComponentApp",
                    "AssetMode": "All",
                    "AssetKind": "Publish",
                    "AssetMergeSource": "",
                    "SourceId": "ComponentApp",
                    "CopyToOutputDirectory": "Never",
                    "Fingerprint": "j6epkkjw1v",
                    "RelatedAsset": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\wwwroot\\app.publish.js",
                    "ContentRoot": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\compressed\\publish\\",
                    "SourceType": "Discovered",
                    "Integrity": "C6haCpkVXnh1QGSy09z8PypGdkYfcOcKjLgzitIJmOQ=",
                    "AssetRole": "Alternative",
                    "AssetMergeBehavior": "",
                    "AssetTraitValue": "br",
                    "AssetTraitName": "Content-Encoding",
                    "OriginalItemSpec": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\wwwroot\\app.publish.js",
                    "CopyToPublishDirectory": "PreserveNewest"
                }
            ]
            """;

        var assetCandidates = JsonSerializer.Deserialize<StaticWebAsset[]>(assetCandidatesJson);

        var endpointCandidatesJson = """
            [
                {
                    "Route": "app.js",
                    "AssetFile": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\compressed\\3jkjor5twg.gz",
                    "Selectors": [{"Name":"Content-Encoding","Value":"gzip","Quality":"0.020408"}],
                    "ResponseHeaders": [{"Name":"Accept-Ranges","Value":"bytes"},{"Name":"Content-Length","Value":"28"},{"Name":"Content-Type","Value":"text/javascript"},{"Name":"ETag","Value":"lClGOfcWqtQdAvO3zCRzZEg/4RmOMbr9/V54QO76j/A="},{"Name":"Last-Modified","Value":"Mon, 01 Apr 2024 15:16:26 GMT"},{"Name":"Accept-Ranges","Value":"bytes"},{"Name":"Content-Length","Value":"28"},{"Name":"ETag","Value":"JBRzZrf2Y9xv2\\u002BOdymyh9B4s0SZRVmPk\\u002BJ1/hXatg84="},{"Name":"Last-Modified","Value":"Mon, 01 Apr 2024 15:16:26 GMT"},{"Name":"Content-Encoding","Value":"gzip"},{"Name":"Vary","Value":"Content-Encoding"}],
                    "EndpointProperties": []
                },
                {
                    "Route": "app.js",
                    "AssetFile": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\compressed\\vdjfplf5i2.gz",
                    "Selectors": [{"Name":"Content-Encoding","Value":"gzip","Quality":"0.017544"}],
                    "ResponseHeaders": [{"Name":"Accept-Ranges","Value":"bytes"},{"Name":"Content-Length","Value":"36"},{"Name":"Content-Type","Value":"text/javascript"},{"Name":"ETag","Value":"YWTY9gwbxmffjp756H8XAAEQ5BoRO1ujOAs\\u002BicruBEI="},{"Name":"Last-Modified","Value":"Mon, 01 Apr 2024 15:16:26 GMT"},{"Name":"Accept-Ranges","Value":"bytes"},{"Name":"Content-Length","Value":"36"},{"Name":"ETag","Value":"MhWnj64DNaFcwJt2dnZR14OYzMypKWOopajvF9gHZoE="},{"Name":"Last-Modified","Value":"Mon, 01 Apr 2024 15:16:26 GMT"},{"Name":"Content-Encoding","Value":"gzip"},{"Name":"Vary","Value":"Content-Encoding"}],
                    "EndpointProperties": []
                },
                {
                    "Route": "app.js",
                    "AssetFile": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\wwwroot\\app.js",
                    "Selectors": [],
                    "ResponseHeaders": [{"Name":"Accept-Ranges","Value":"bytes"},{"Name":"Content-Length","Value":"28"},{"Name":"Content-Type","Value":"text/javascript"},{"Name":"ETag","Value":"lClGOfcWqtQdAvO3zCRzZEg/4RmOMbr9/V54QO76j/A="},{"Name":"Last-Modified","Value":"Mon, 01 Apr 2024 15:16:26 GMT"}],
                    "EndpointProperties": []
                },
                {
                    "Route": "app.js",
                    "AssetFile": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\wwwroot\\app.publish.js",
                    "Selectors": [],
                    "ResponseHeaders": [{"Name":"Accept-Ranges","Value":"bytes"},{"Name":"Content-Length","Value":"36"},{"Name":"Content-Type","Value":"text/javascript"},{"Name":"ETag","Value":"YWTY9gwbxmffjp756H8XAAEQ5BoRO1ujOAs\\u002BicruBEI="},{"Name":"Last-Modified","Value":"Mon, 01 Apr 2024 15:16:26 GMT"}],
                    "EndpointProperties": []
                },
                {
                    "Route": "app.js.gz",
                    "AssetFile": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\compressed\\3jkjor5twg.gz",
                    "Selectors": [],
                    "ResponseHeaders": [{"Name":"Accept-Ranges","Value":"bytes"},{"Name":"Content-Length","Value":"28"},{"Name":"Content-Type","Value":"text/javascript"},{"Name":"ETag","Value":"JBRzZrf2Y9xv2\\u002BOdymyh9B4s0SZRVmPk\\u002BJ1/hXatg84="},{"Name":"Last-Modified","Value":"Mon, 01 Apr 2024 15:16:26 GMT"},{"Name":"Content-Encoding","Value":"gzip"},{"Name":"Vary","Value":"Content-Encoding"}],
                    "EndpointProperties": []
                },
                {
                    "Route": "app.js.gz",
                    "AssetFile": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\compressed\\vdjfplf5i2.gz",
                    "Selectors": [],
                    "ResponseHeaders": [{"Name":"Accept-Ranges","Value":"bytes"},{"Name":"Content-Length","Value":"36"},{"Name":"Content-Type","Value":"text/javascript"},{"Name":"ETag","Value":"MhWnj64DNaFcwJt2dnZR14OYzMypKWOopajvF9gHZoE="},{"Name":"Last-Modified","Value":"Mon, 01 Apr 2024 15:16:26 GMT"},{"Name":"Content-Encoding","Value":"gzip"},{"Name":"Vary","Value":"Content-Encoding"}],
                    "EndpointProperties": []
                },
                {
                    "Route": "ComponentApp.bundle.scp.css",
                    "AssetFile": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\compressed\\8j96a56wcg.gz",
                    "Selectors": [{"Name":"Content-Encoding","Value":"gzip","Quality":"0.005917"}],
                    "ResponseHeaders": [{"Name":"Accept-Ranges","Value":"bytes"},{"Name":"Content-Length","Value":"234"},{"Name":"Content-Type","Value":"text/css"},{"Name":"ETag","Value":"TlbZkCZHJzdAKFf3fWLJDNxplhFXy9gSrAReEBDlTvA="},{"Name":"Last-Modified","Value":"Mon, 01 Apr 2024 15:16:28 GMT"},{"Name":"Accept-Ranges","Value":"bytes"},{"Name":"Content-Length","Value":"234"},{"Name":"ETag","Value":"PsrlWTzC48XUSMKZ9t\\u002BHhgNHPzmN/sE0e7MCtaW0Ve8="},{"Name":"Last-Modified","Value":"Mon, 01 Apr 2024 15:16:28 GMT"},{"Name":"Content-Encoding","Value":"gzip"},{"Name":"Vary","Value":"Content-Encoding"}],
                    "EndpointProperties": []
                },
                {
                    "Route": "ComponentApp.bundle.scp.css",
                    "AssetFile": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\scopedcss\\projectbundle\\ComponentApp.bundle.scp.css",
                    "Selectors": [],
                    "ResponseHeaders": [{"Name":"Accept-Ranges","Value":"bytes"},{"Name":"Content-Length","Value":"234"},{"Name":"Content-Type","Value":"text/css"},{"Name":"ETag","Value":"TlbZkCZHJzdAKFf3fWLJDNxplhFXy9gSrAReEBDlTvA="},{"Name":"Last-Modified","Value":"Mon, 01 Apr 2024 15:16:28 GMT"}],
                    "EndpointProperties": []
                },
                {
                    "Route": "ComponentApp.bundle.scp.css.gz",
                    "AssetFile": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\compressed\\8j96a56wcg.gz",
                    "Selectors": [],
                    "ResponseHeaders": [{"Name":"Accept-Ranges","Value":"bytes"},{"Name":"Content-Length","Value":"234"},{"Name":"Content-Type","Value":"text/css"},{"Name":"ETag","Value":"PsrlWTzC48XUSMKZ9t\\u002BHhgNHPzmN/sE0e7MCtaW0Ve8="},{"Name":"Last-Modified","Value":"Mon, 01 Apr 2024 15:16:28 GMT"},{"Name":"Content-Encoding","Value":"gzip"},{"Name":"Vary","Value":"Content-Encoding"}],
                    "EndpointProperties": []
                },
                {
                    "Route": "ComponentApp.styles.css",
                    "AssetFile": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\compressed\\k3svc14xi8.gz",
                    "Selectors": [{"Name":"Content-Encoding","Value":"gzip","Quality":"0.005917"}],
                    "ResponseHeaders": [{"Name":"Accept-Ranges","Value":"bytes"},{"Name":"Content-Length","Value":"234"},{"Name":"Content-Type","Value":"text/css"},{"Name":"ETag","Value":"TlbZkCZHJzdAKFf3fWLJDNxplhFXy9gSrAReEBDlTvA="},{"Name":"Last-Modified","Value":"Mon, 01 Apr 2024 15:16:28 GMT"},{"Name":"Accept-Ranges","Value":"bytes"},{"Name":"Content-Length","Value":"234"},{"Name":"ETag","Value":"PsrlWTzC48XUSMKZ9t\\u002BHhgNHPzmN/sE0e7MCtaW0Ve8="},{"Name":"Last-Modified","Value":"Mon, 01 Apr 2024 15:16:28 GMT"},{"Name":"Content-Encoding","Value":"gzip"},{"Name":"Vary","Value":"Content-Encoding"}],
                    "EndpointProperties": []
                },
                {
                    "Route": "ComponentApp.styles.css",
                    "AssetFile": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\scopedcss\\bundle\\ComponentApp.styles.css",
                    "Selectors": [],
                    "ResponseHeaders": [{"Name":"Accept-Ranges","Value":"bytes"},{"Name":"Content-Length","Value":"234"},{"Name":"Content-Type","Value":"text/css"},{"Name":"ETag","Value":"TlbZkCZHJzdAKFf3fWLJDNxplhFXy9gSrAReEBDlTvA="},{"Name":"Last-Modified","Value":"Mon, 01 Apr 2024 15:16:28 GMT"}],
                    "EndpointProperties": []
                },
                {
                    "Route": "ComponentApp.styles.css.gz",
                    "AssetFile": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\compressed\\k3svc14xi8.gz",
                    "Selectors": [],
                    "ResponseHeaders": [{"Name":"Accept-Ranges","Value":"bytes"},{"Name":"Content-Length","Value":"234"},{"Name":"Content-Type","Value":"text/css"},{"Name":"ETag","Value":"PsrlWTzC48XUSMKZ9t\\u002BHhgNHPzmN/sE0e7MCtaW0Ve8="},{"Name":"Last-Modified","Value":"Mon, 01 Apr 2024 15:16:28 GMT"},{"Name":"Content-Encoding","Value":"gzip"},{"Name":"Vary","Value":"Content-Encoding"}],
                    "EndpointProperties": []
                },
                {
                    "Route": "ComponentApp.styles.css.br",
                    "AssetFile": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\compressed\\publish\\k3svc14xi8.br",
                    "Selectors": [],
                    "ResponseHeaders": [{"Name":"Accept-Ranges","Value":"bytes"},{"Name":"Content-Length","Value":"234"},{"Name":"Content-Type","Value":"text/css"},{"Name":"ETag","Value":"LWnMpOyUHO4LpFOxNrhon9FCyblI5rQaMgyJPfjvZtg="},{"Name":"Last-Modified","Value":"Mon, 01 Apr 2024 15:16:28 GMT"}],
                    "EndpointProperties": []
                },
                {
                    "Route": "ComponentApp.bundle.scp.css.br",
                    "AssetFile": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\compressed\\publish\\8j96a56wcg.br",
                    "Selectors": [],
                    "ResponseHeaders": [{"Name":"Accept-Ranges","Value":"bytes"},{"Name":"Content-Length","Value":"234"},{"Name":"Content-Type","Value":"text/css"},{"Name":"ETag","Value":"LWnMpOyUHO4LpFOxNrhon9FCyblI5rQaMgyJPfjvZtg="},{"Name":"Last-Modified","Value":"Mon, 01 Apr 2024 15:16:28 GMT"}],
                    "EndpointProperties": []
                },
                {
                    "Route": "app.js.br",
                    "AssetFile": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\compressed\\publish\\vdjfplf5i2.br",
                    "Selectors": [],
                    "ResponseHeaders": [{"Name":"Accept-Ranges","Value":"bytes"},{"Name":"Content-Length","Value":"36"},{"Name":"Content-Type","Value":"text/javascript"},{"Name":"ETag","Value":"C6haCpkVXnh1QGSy09z8PypGdkYfcOcKjLgzitIJmOQ="},{"Name":"Last-Modified","Value":"Mon, 01 Apr 2024 15:16:26 GMT"}],
                    "EndpointProperties": []
                }
            ]            
            """;

        var endpointCandidates = JsonSerializer.Deserialize<StaticWebAssetEndpoint[]>(endpointCandidatesJson);
        Assert.NotNull(assetCandidates);
        Assert.NotNull(endpointCandidates);

        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var task = new ApplyCompressionNegotiation
        {
            BuildEngine = buildEngine.Object,
            CandidateAssets = assetCandidates.Select(a => a.ToTaskItem()).ToArray(),
            CandidateEndpoints = endpointCandidates.Select(a => a.ToTaskItem()).ToArray(),
            TestResolveFileLength = value => 100
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().Be(true);
    }

    [Fact]
    public void EncodingNegotiation_Integration_Minimal()
    {
        var assetCandidatesJson = """
            [
                {
                    "Identity": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\compressed\\vdjfplf5i2.gz",
                    "RelativePath": "app.js.gz",
                    "BasePath": "_content/ComponentApp",
                    "AssetMode": "All",
                    "AssetKind": "Publish",
                    "AssetMergeSource": "",
                    "SourceId": "ComponentApp",
                    "CopyToOutputDirectory": "Never",
                    "Fingerprint": "u9d8oeh4c0",
                    "RelatedAsset": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\wwwroot\\app.publish.js",
                    "ContentRoot": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\compressed\\",
                    "SourceType": "Discovered",
                    "Integrity": "MhWnj64DNaFcwJt2dnZR14OYzMypKWOopajvF9gHZoE=",
                    "AssetRole": "Alternative",
                    "AssetMergeBehavior": "",
                    "AssetTraitValue": "gzip",
                    "AssetTraitName": "Content-Encoding",
                    "OriginalItemSpec": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\wwwroot\\app.publish.js",
                    "CopyToPublishDirectory": "PreserveNewest"
                },
                {
                    "Identity": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\wwwroot\\app.publish.js",
                    "RelativePath": "app.js",
                    "BasePath": "_content/ComponentApp",
                    "AssetMode": "All",
                    "AssetKind": "Publish",
                    "AssetMergeSource": "",
                    "SourceId": "ComponentApp",
                    "CopyToOutputDirectory": "Never",
                    "Fingerprint": "r1ue4mnokm",
                    "RelatedAsset": "",
                    "ContentRoot": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\wwwroot\\",
                    "SourceType": "Discovered",
                    "Integrity": "YWTY9gwbxmffjp756H8XAAEQ5BoRO1ujOAs+icruBEI=",
                    "AssetRole": "Primary",
                    "AssetMergeBehavior": "",
                    "AssetTraitValue": "",
                    "AssetTraitName": "",
                    "OriginalItemSpec": "wwwroot\\app.publish.js",
                    "CopyToPublishDirectory": "PreserveNewest"
                },
                {
                    "Identity": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\compressed\\publish\\vdjfplf5i2.br",
                    "RelativePath": "app.js.br",
                    "BasePath": "_content/ComponentApp",
                    "AssetMode": "All",
                    "AssetKind": "Publish",
                    "AssetMergeSource": "",
                    "SourceId": "ComponentApp",
                    "CopyToOutputDirectory": "Never",
                    "Fingerprint": "j6epkkjw1v",
                    "RelatedAsset": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\wwwroot\\app.publish.js",
                    "ContentRoot": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\compressed\\publish\\",
                    "SourceType": "Discovered",
                    "Integrity": "C6haCpkVXnh1QGSy09z8PypGdkYfcOcKjLgzitIJmOQ=",
                    "AssetRole": "Alternative",
                    "AssetMergeBehavior": "",
                    "AssetTraitValue": "br",
                    "AssetTraitName": "Content-Encoding",
                    "OriginalItemSpec": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\wwwroot\\app.publish.js",
                    "CopyToPublishDirectory": "PreserveNewest"
                }
            ]
            """;

        var assetCandidates = JsonSerializer.Deserialize<StaticWebAsset[]>(assetCandidatesJson);

        var endpointCandidatesJson = """
            [
                {
                    "Route": "app.js",
                    "AssetFile": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\compressed\\3jkjor5twg.gz",
                    "Selectors": [{"Name":"Content-Encoding","Value":"gzip","Quality":"0.020408"}],
                    "ResponseHeaders": [{"Name":"Accept-Ranges","Value":"bytes"},{"Name":"Content-Length","Value":"28"},{"Name":"Content-Type","Value":"text/javascript"},{"Name":"ETag","Value":"lClGOfcWqtQdAvO3zCRzZEg/4RmOMbr9/V54QO76j/A="},{"Name":"Last-Modified","Value":"Mon, 01 Apr 2024 15:16:26 GMT"},{"Name":"Accept-Ranges","Value":"bytes"},{"Name":"Content-Length","Value":"28"},{"Name":"ETag","Value":"JBRzZrf2Y9xv2\\u002BOdymyh9B4s0SZRVmPk\\u002BJ1/hXatg84="},{"Name":"Last-Modified","Value":"Mon, 01 Apr 2024 15:16:26 GMT"},{"Name":"Content-Encoding","Value":"gzip"},{"Name":"Vary","Value":"Content-Encoding"}],
                    "EndpointProperties": []
                },
                {
                    "Route": "app.js",
                    "AssetFile": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\compressed\\vdjfplf5i2.gz",
                    "Selectors": [{"Name":"Content-Encoding","Value":"gzip","Quality":"0.017544"}],
                    "ResponseHeaders": [{"Name":"Accept-Ranges","Value":"bytes"},{"Name":"Content-Length","Value":"36"},{"Name":"Content-Type","Value":"text/javascript"},{"Name":"ETag","Value":"YWTY9gwbxmffjp756H8XAAEQ5BoRO1ujOAs\\u002BicruBEI="},{"Name":"Last-Modified","Value":"Mon, 01 Apr 2024 15:16:26 GMT"},{"Name":"Accept-Ranges","Value":"bytes"},{"Name":"Content-Length","Value":"36"},{"Name":"ETag","Value":"MhWnj64DNaFcwJt2dnZR14OYzMypKWOopajvF9gHZoE="},{"Name":"Last-Modified","Value":"Mon, 01 Apr 2024 15:16:26 GMT"},{"Name":"Content-Encoding","Value":"gzip"},{"Name":"Vary","Value":"Content-Encoding"}],
                    "EndpointProperties": []
                },
                {
                    "Route": "app.js",
                    "AssetFile": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\wwwroot\\app.js",
                    "Selectors": [],
                    "ResponseHeaders": [{"Name":"Accept-Ranges","Value":"bytes"},{"Name":"Content-Length","Value":"28"},{"Name":"Content-Type","Value":"text/javascript"},{"Name":"ETag","Value":"lClGOfcWqtQdAvO3zCRzZEg/4RmOMbr9/V54QO76j/A="},{"Name":"Last-Modified","Value":"Mon, 01 Apr 2024 15:16:26 GMT"}],
                    "EndpointProperties": []
                },
                {
                    "Route": "app.js",
                    "AssetFile": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\wwwroot\\app.publish.js",
                    "Selectors": [],
                    "ResponseHeaders": [{"Name":"Accept-Ranges","Value":"bytes"},{"Name":"Content-Length","Value":"36"},{"Name":"Content-Type","Value":"text/javascript"},{"Name":"ETag","Value":"YWTY9gwbxmffjp756H8XAAEQ5BoRO1ujOAs\\u002BicruBEI="},{"Name":"Last-Modified","Value":"Mon, 01 Apr 2024 15:16:26 GMT"}],
                    "EndpointProperties": []
                },
                {
                    "Route": "app.js.gz",
                    "AssetFile": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\compressed\\3jkjor5twg.gz",
                    "Selectors": [],
                    "ResponseHeaders": [{"Name":"Accept-Ranges","Value":"bytes"},{"Name":"Content-Length","Value":"28"},{"Name":"Content-Type","Value":"text/javascript"},{"Name":"ETag","Value":"JBRzZrf2Y9xv2\\u002BOdymyh9B4s0SZRVmPk\\u002BJ1/hXatg84="},{"Name":"Last-Modified","Value":"Mon, 01 Apr 2024 15:16:26 GMT"},{"Name":"Content-Encoding","Value":"gzip"},{"Name":"Vary","Value":"Content-Encoding"}],
                    "EndpointProperties": []
                },
                {
                    "Route": "app.js.gz",
                    "AssetFile": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\compressed\\vdjfplf5i2.gz",
                    "Selectors": [],
                    "ResponseHeaders": [{"Name":"Accept-Ranges","Value":"bytes"},{"Name":"Content-Length","Value":"36"},{"Name":"Content-Type","Value":"text/javascript"},{"Name":"ETag","Value":"MhWnj64DNaFcwJt2dnZR14OYzMypKWOopajvF9gHZoE="},{"Name":"Last-Modified","Value":"Mon, 01 Apr 2024 15:16:26 GMT"},{"Name":"Content-Encoding","Value":"gzip"},{"Name":"Vary","Value":"Content-Encoding"}],
                    "EndpointProperties": []
                },
                {
                    "Route": "app.js.br",
                    "AssetFile": "C:\\work\\dotnet-sdk\\artifacts\\tmp\\Debug\\Publish_Creat---46230AA4\\obj\\Debug\\net9.0\\compressed\\publish\\vdjfplf5i2.br",
                    "Selectors": [],
                    "ResponseHeaders": [{"Name":"Accept-Ranges","Value":"bytes"},{"Name":"Content-Length","Value":"36"},{"Name":"Content-Type","Value":"text/javascript"},{"Name":"ETag","Value":"C6haCpkVXnh1QGSy09z8PypGdkYfcOcKjLgzitIJmOQ="},{"Name":"Last-Modified","Value":"Mon, 01 Apr 2024 15:16:26 GMT"}],
                    "EndpointProperties": []
                }
            ]            
            """;

        var endpointCandidates = JsonSerializer.Deserialize<StaticWebAssetEndpoint[]>(endpointCandidatesJson);
        Assert.NotNull(assetCandidates);
        Assert.NotNull(endpointCandidates);

        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var task = new ApplyCompressionNegotiation
        {
            BuildEngine = buildEngine.Object,
            CandidateAssets = assetCandidates.Select(a => a.ToTaskItem()).ToArray(),
            CandidateEndpoints = endpointCandidates.Select(a => a.ToTaskItem()).ToArray(),
            TestResolveFileLength = value => 100
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().Be(true);
    }


    private StaticWebAssetEndpointResponseHeader[] CreateHeaders(string contentType)
    {
        return
        [
            new StaticWebAssetEndpointResponseHeader {
                Name = "Content-Type",
                Value = contentType
            }
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
