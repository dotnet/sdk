// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Sdk.Razor.Tests.StaticWebAssets;

public class FingerprintPatternMatcherTest
{
    private readonly TaskLoggingHelper _log = new TestTaskLoggingHelper();

    [Fact]
    public void AppendFingerprintPattern_AlreadyContainsFingerprint_ReturnsIdentity()
    {
        // Arrange
        var relativePath = "test#[.{fingerprint}].txt";

        // Act
        var result = new FingerprintPatternMatcher(_log, []).AppendFingerprintPattern(CreateMatchContext(relativePath), "Identity");

        // Assert
        Assert.Equal(relativePath, result);
    }

    [Fact]
    public void AppendFingerprintPattern_AppendsPattern_AtTheEndOfTheFileName()
    {
        // Arrange
        var relativePath = Path.Combine("folder", "test.txt");
        var expected = Path.Combine("folder", "test#[.{fingerprint}]?.txt");

        // Act
        var result = new FingerprintPatternMatcher(_log, []).AppendFingerprintPattern(CreateMatchContext(relativePath), "Identity");

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void AppendFingerprintPattern_AppendsPattern_AtTheEndOfTheFileName_WhenFileNameContainsDots()
    {
        // Arrange
        var relativePath = Path.Combine("folder", "test.v1.txt");
        var expected = Path.Combine("folder", "test.v1#[.{fingerprint}]?.txt");
        // Act
        var result = new FingerprintPatternMatcher(_log, []).AppendFingerprintPattern(CreateMatchContext(relativePath), "Identity");
        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void AppendFingerprintPattern_AppendsPattern_AtTheEndOfTheFileName_WhenFileDoesNotHaveExtension()
    {
        // Arrange
        var relativePath = Path.Combine("folder", "README");
        var expected = Path.Combine("folder", "README#[.{fingerprint}]?");
        // Act
        var result = new FingerprintPatternMatcher(_log, []).AppendFingerprintPattern(CreateMatchContext(relativePath), "Identity");
        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void AppendFingerprintPattern_AppendsPattern_AtTheRightLocation_WhenACustomPatternIsProvided()
    {
        // Arrange
        var relativePath = Path.Combine("folder", "test.bundle.scp.css");
        var expected = Path.Combine("folder", "test#[.{fingerprint}]!.bundle.scp.css");

        // Act
        var result = new FingerprintPatternMatcher(
            _log,
            [new TaskItem("ScopedCSS", new Dictionary<string, string> { ["Pattern"] = "*.bundle.scp.css", ["Expression"] = "#[.{fingerprint}]!" })])
            .AppendFingerprintPattern(CreateMatchContext(relativePath), "Identity");

        // Assert
        Assert.Equal(expected, result);
    }

    private StaticWebAssetGlobMatcher.MatchContext CreateMatchContext(string path)
    {
        var context = new StaticWebAssetGlobMatcher.MatchContext();
        context.SetPathAndReinitialize(path);
        return context;
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
