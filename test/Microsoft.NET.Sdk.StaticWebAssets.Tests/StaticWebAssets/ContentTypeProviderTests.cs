// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Utilities;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests.StaticWebAssets;

[TestClass]

public class ContentTypeProviderTests
{
    private readonly TaskLoggingHelper _log = new TestTaskLoggingHelper();

    [TestMethod]
    public void GetContentType_ReturnsTextPlainForTextFiles()
    {
        // Arrange
        var provider = new ContentTypeProvider([]);

        // Act
        var contentType = provider.ResolveContentTypeMapping(CreateContext("Fake-License.txt"), _log);

        // Assert
        Assert.AreEqual("text/plain", contentType.MimeType);
    }

    [TestMethod]
    public void GetContentType_ReturnsMappingForRelativePath()
    {
        // Arrange
        var provider = new ContentTypeProvider([]);

        // Act
        var contentType = provider.ResolveContentTypeMapping(CreateContext("Components/Pages/Counter.razor.js"), _log);

        // Assert
        Assert.AreEqual("text/javascript", contentType.MimeType);
    }

    private StaticWebAssetGlobMatcher.MatchContext CreateContext(string v)
    {
        var ctx = StaticWebAssetGlobMatcher.CreateMatchContext();
        ctx.SetPathAndReinitialize(v);
        return ctx;
    }

    // wwwroot\exampleJsInterop.js.gz

    [TestMethod]
    public void GetContentType_ReturnsMappingForCompressedRelativePath()
    {
        // Arrange
        var provider = new ContentTypeProvider([]);

        // Act
        var contentType = provider.ResolveContentTypeMapping(CreateContext("wwwroot/exampleJsInterop.js.gz"), _log);

        // Assert
        Assert.AreEqual("text/javascript", contentType.MimeType);
    }

    [TestMethod]
    public void GetContentType_HandlesFingerprintedPaths()
    {
        // Arrange
        var provider = new ContentTypeProvider([]);
        // Act
        var contentType = provider.ResolveContentTypeMapping(CreateContext("_content/RazorPackageLibraryDirectDependency/RazorPackageLibraryDirectDependency#[.{fingerprint}].bundle.scp.css.gz"), _log);
        // Assert
        Assert.AreEqual("text/css", contentType.MimeType);
    }

    [TestMethod]
    public void GetContentType_ReturnsDefaultForUnknownMappings()
    {
        // Arrange
        var provider = new ContentTypeProvider([]);

        // Act
        var contentType = provider.ResolveContentTypeMapping(CreateContext("something.unknown"), _log);

        // Assert
        Assert.IsNull(contentType.MimeType);
    }

    [TestMethod]
    [DataRow("something.unknown.gz", "application/x-gzip")]
    [DataRow("something.unknown.br", "application/octet-stream")]
    public void GetContentType_ReturnsGzipOrBrotliForUnknownCompressedMappings(string path, string expectedMapping)
    {
        // Arrange
        var provider = new ContentTypeProvider([]);

        // Act
        var contentType = provider.ResolveContentTypeMapping(CreateContext(path), _log);

        // Assert
        Assert.AreEqual(expectedMapping, contentType.MimeType);
    }

    [TestMethod]
    [DataRow("Fake-License.txt.gz")]
    [DataRow("Fake-License.txt.br")]
    public void GetContentType_ReturnsTextPlainForCompressedTextFiles(string path)
    {
        // Arrange
        var provider = new ContentTypeProvider([]);

        // Act
        var contentType = provider.ResolveContentTypeMapping(CreateContext(path), _log);

        // Assert
        Assert.AreEqual("text/plain", contentType.MimeType);
    }

    [TestMethod]
    public void GetContentType_CustomMappingOverridesBuiltInMapping()
    {
        // Arrange
        var customMapping = new ContentTypeMapping("text/html", "no-store, must-revalidate, no-cache", "*.html", 2);
        var provider = new ContentTypeProvider([customMapping]);

        // Act
        var contentType = provider.ResolveContentTypeMapping(CreateContext("index.html"), _log);

        // Assert
        Assert.AreEqual("text/html", contentType.MimeType);
        Assert.AreEqual("no-store, must-revalidate, no-cache", contentType.Cache);
        Assert.AreEqual("*.html", contentType.Pattern);
        Assert.AreEqual(2, contentType.Priority);
    }

    [TestMethod]
    public void GetContentType_CustomMappingOverridesBuiltInMappingForCompressedFiles()
    {
        // Arrange
        var customMapping = new ContentTypeMapping("text/html", "no-store, must-revalidate, no-cache", "*.html", 2);
        var provider = new ContentTypeProvider([customMapping]);

        // Act
        var contentType = provider.ResolveContentTypeMapping(CreateContext("index.html.gz"), _log);

        // Assert
        Assert.AreEqual("text/html", contentType.MimeType);
        Assert.AreEqual("no-store, must-revalidate, no-cache", contentType.Cache);
        Assert.AreEqual("*.html", contentType.Pattern);
        Assert.AreEqual(2, contentType.Priority);
    }

    [TestMethod]
    public void GetContentType_CustomJavaScriptMappingOverridesBuiltIn()
    {
        // Arrange
        var customMapping = new ContentTypeMapping("text/javascript", "max-age=3600", "*.js", 3);
        var provider = new ContentTypeProvider([customMapping]);

        // Act
        var contentType = provider.ResolveContentTypeMapping(CreateContext("app.js"), _log);

        // Assert
        Assert.AreEqual("text/javascript", contentType.MimeType);
        Assert.AreEqual("max-age=3600", contentType.Cache);
        Assert.AreEqual("*.js", contentType.Pattern);
        Assert.AreEqual(3, contentType.Priority);
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
