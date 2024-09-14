// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.NET.Sdk.StaticWebAssets.Tasks;
using Moq;
using NuGet.ContentModel;

namespace Microsoft.NET.Sdk.Razor.Tests.StaticWebAssets;

public class GenerateStaticWebAssetEndpointsPropsFileTest
{
    [Fact]
    public void Generates_ValidEndpointsDefinitions()
    {
        // Arrange
        var file = Path.GetTempFileName();
        var expectedDocument = """
<Project>
  <ItemGroup>
    <StaticWebAssetEndpoint Include="js/sample.js">
      <AssetFile>$([System.IO.Path]::GetFullPath($(MSBuildThisFileDirectory)..\staticwebassets\js\sample.js))</AssetFile>
      <Selectors><![CDATA[[{"Name":"Content-Encoding","Value":"gzip","Quality":"0.1"}]]]></Selectors>
      <EndpointProperties><![CDATA[[{"Name":"integrity","Value":"__integrity__"}]]]></EndpointProperties>
      <ResponseHeaders><![CDATA[[{"Name":"Accept-Ranges","Value":"bytes"},{"Name":"Content-Length","Value":"10"}]]]></ResponseHeaders>
    </StaticWebAssetEndpoint>
  </ItemGroup>
</Project>
""";

        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var task = new GenerateStaticWebAssetEndpointsPropsFile
        {
            BuildEngine = buildEngine.Object,
            StaticWebAssets =
            [
                    CreateStaticWebAsset(
                        Path.Combine("wwwroot","js","sample.js"),
                        "MyLibrary",
                        "Discovered",
                        Path.Combine("js", "sample.js"),
                        "All",
                        "All")
            ],
            StaticWebAssetEndpoints =
            [
                CreateStaticWebAssetEndpoint(
                    Path.Combine("js", "sample.js"),
                    Path.GetFullPath(Path.Combine("wwwroot","js","sample.js")),
                    [
                        new StaticWebAssetEndpointResponseHeader
                        {
                            Name = "Accept-Ranges",
                            Value = "bytes"
                        },
                        new StaticWebAssetEndpointResponseHeader
                        {
                            Name = "Content-Length",
                            Value = "10"
                        }
                    ],
                    [
                        new StaticWebAssetEndpointSelector
                        {
                            Name = "Content-Encoding",
                            Value = "gzip",
                            Quality = "0.1"
                        }
                    ],
                    [
                        new StaticWebAssetEndpointProperty
                        {
                            Name = "integrity",
                            Value = "__integrity__"
                        }
                    ])
            ],
            PackagePathPrefix = "staticwebassets",
            TargetPropsFilePath = file
        };

        // Act
        try
        {
            var result = task.Execute();

            result.Should().BeTrue();
            new FileInfo(file).Should().Exist();
            var document = File.ReadAllText(file);
            document.Should().BeVisuallyEquivalentTo(expectedDocument);
        }
        finally
        {
            if (File.Exists(file))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                }
            }
        }
    }

    [Fact]
    public void Fails_WhenEndpointWithoutAssetExists()
    {
        // Arrange
        var errorMessages = new List<string>();
        var buildEngine = new Mock<IBuildEngine>();
        buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

        var task = new GenerateStaticWebAssetEndpointsPropsFile
        {
            BuildEngine = buildEngine.Object,
            StaticWebAssets = [],
            StaticWebAssetEndpoints =
            [
                CreateStaticWebAssetEndpoint(
                    Path.Combine("js", "sample.js").Replace('\\', '/'),
                    Path.GetFullPath(Path.Combine("wwwroot","js","sample.js")),
                    [
                        new StaticWebAssetEndpointResponseHeader
                        {
                            Name = "Accept-Ranges",
                            Value = "bytes"
                        },
                        new StaticWebAssetEndpointResponseHeader
                        {
                            Name = "Content-Length",
                            Value = "10"
                        }
                    ],
                    [
                        new StaticWebAssetEndpointSelector
                        {
                            Name = "Content-Encoding",
                            Value = "gzip",
                            Quality = "0.1"
                        }
                    ],
                    [
                        new StaticWebAssetEndpointProperty
                        {
                            Name = "integrity",
                            Value = "__integrity__"
                        }
                    ])
            ],
            PackagePathPrefix = "staticwebassets",
            TargetPropsFilePath = Path.GetTempFileName(),
        };

        // Act
        var result = task.Execute();

        result.Should().BeFalse();
        errorMessages.Should().ContainSingle();
        errorMessages[0].Should().Be($"""The asset file '{Path.GetFullPath(Path.Combine("wwwroot", "js", "sample.js"))}' specified in the endpoint '{Path.Combine("js","sample.js").Replace('\\', '/')}' does not exist.""");
    }

    private ITaskItem CreateStaticWebAsset(
        string itemSpec,
        string sourceId,
        string sourceType,
        string relativePath,
        string assetKind,
        string assetMode)
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
            RelatedAsset = "",
            AssetTraitName = "",
            AssetTraitValue = "",
            CopyToOutputDirectory = "",
            CopyToPublishDirectory = "",
            OriginalItemSpec = itemSpec,
            // Add these to avoid accessing the disk to compute them
            Integrity = "integrity",
            Fingerprint = "fingerprint",
        };

        result.ApplyDefaults();
        result.Normalize();

        return result.ToTaskItem();
    }

    private ITaskItem CreateStaticWebAssetEndpoint(
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
