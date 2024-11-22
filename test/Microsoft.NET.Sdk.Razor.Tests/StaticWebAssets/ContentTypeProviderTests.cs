// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Sdk.Razor.Tests.StaticWebAssets;

public class ContentTypeProviderTests
{
    private readonly TaskLoggingHelper _log = new TestTaskLoggingHelper();

    [Fact]
    public void GetContentType_ReturnsTextPlainForTextFiles()
    {
        // Arrange
        var provider = new ContentTypeProvider([]);

        // Act
        var contentType = provider.ResolveContentTypeMapping(CreateContext("Fake-License.txt"), _log);

        // Assert
        Assert.Equal("text/plain", contentType.MimeType);
    }

    [Fact]
    public void GetContentType_ReturnsMappingForRelativePath()
    {
        // Arrange
        var provider = new ContentTypeProvider([]);

        // Act
        var contentType = provider.ResolveContentTypeMapping(CreateContext("Components/Pages/Counter.razor.js"), _log);

        // Assert
        Assert.Equal("text/javascript", contentType.MimeType);
    }

    private StaticWebAssetGlobMatcher.MatchContext CreateContext(string v)
    {
        var ctx = StaticWebAssetGlobMatcher.CreateMatchContext();
        ctx.SetPathAndReinitialize(v);
        return ctx;
    }

    // wwwroot\exampleJsInterop.js.gz

    [Fact]
    public void GetContentType_ReturnsMappingForCompressedRelativePath()
    {
        // Arrange
        var provider = new ContentTypeProvider([]);

        // Act
        var contentType = provider.ResolveContentTypeMapping(CreateContext("wwwroot/exampleJsInterop.js.gz"), _log);

        // Assert
        Assert.Equal("text/javascript", contentType.MimeType);
    }

    [Fact]
    public void GetContentType_HandlesFingerprintedPaths()
    {
        // Arrange
        var provider = new ContentTypeProvider([]);
        // Act
        var contentType = provider.ResolveContentTypeMapping(CreateContext("_content/RazorPackageLibraryDirectDependency/RazorPackageLibraryDirectDependency#[.{fingerprint}].bundle.scp.css.gz"), _log);
        // Assert
        Assert.Equal("text/css", contentType.MimeType);
    }

    [Fact]
    public void GetContentType_ReturnsDefaultForUnknownMappings()
    {
        // Arrange
        var provider = new ContentTypeProvider([]);

        // Act
        var contentType = provider.ResolveContentTypeMapping(CreateContext("something.unknown"), _log);

        // Assert
        Assert.Null(contentType.MimeType);
    }

    [Theory]
    [InlineData("something.unknown.gz", "application/x-gzip")]
    [InlineData("something.unknown.br", "application/octet-stream")]
    public void GetContentType_ReturnsGzipOrBrotliForUnknownCompressedMappings(string path, string expectedMapping)
    {
        // Arrange
        var provider = new ContentTypeProvider([]);

        // Act
        var contentType = provider.ResolveContentTypeMapping(CreateContext(path), _log);

        // Assert
        Assert.Equal(expectedMapping, contentType.MimeType);
    }

    [Theory]
    [InlineData("Fake-License.txt.gz")]
    [InlineData("Fake-License.txt.br")]
    public void GetContentType_ReturnsTextPlainForCompressedTextFiles(string path)
    {
        // Arrange
        var provider = new ContentTypeProvider([]);

        // Act
        var contentType = provider.ResolveContentTypeMapping(CreateContext(path), _log);

        // Assert
        Assert.Equal("text/plain", contentType.MimeType);
    }

    private class TestTaskLoggingHelper : TaskLoggingHelper
    {
        public TestTaskLoggingHelper() : base(new TestTask())
        {
        }

        private class TestTask : ITask
        {
            public IBuildEngine BuildEngine { get; set; } = new TestBuildEngine();
            public ITaskHost HostObject { get; set; } = new TestTaskHost();

            public bool Execute() => true;
        }

        private class TestBuildEngine : IBuildEngine
        {
            public bool ContinueOnError => true;

            public int LineNumberOfTaskNode => 0;

            public int ColumnNumberOfTaskNode => 0;

            public string ProjectFileOfTaskNode => "test.csproj";

            public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs) => true;

            public void LogCustomEvent(CustomBuildEventArgs e) { }
            public void LogErrorEvent(BuildErrorEventArgs e) { }
            public void LogMessageEvent(BuildMessageEventArgs e) { }
            public void LogWarningEvent(BuildWarningEventArgs e) { }
        }

        private class TestTaskHost : ITaskHost
        {
            public object HostObject { get; set; } = new object();
        }
    }

}
