// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.DotNet.MSBuildSdkResolver;

namespace Microsoft.DotNet.Cli.Utils.Tests;

public class GivenAFileBasedSdkResolver
{
    private readonly ITestOutputHelper _logger;

    public GivenAFileBasedSdkResolver(ITestOutputHelper logger)
    {
        _logger = logger;
    }

    [Fact]
    public void ItHasCorrectNameAndPriority()
    {
        var resolver = new FileBasedSdkResolver();

        Assert.Equal(4500, resolver.Priority);
        Assert.Equal("Microsoft.DotNet.FileBasedSdkResolver", resolver.Name);
    }

    [Fact]
    public void ItResolvesRelativePathSdk()
    {
        // Create a temporary SDK directory structure
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-sdk-{Guid.NewGuid()}");
        var sdkDir = Path.Combine(tempDir, "TestSdk", "Sdk");
        Directory.CreateDirectory(sdkDir);

        try
        {
            // Create Sdk.props and Sdk.targets
            File.WriteAllText(Path.Combine(sdkDir, "Sdk.props"), "<Project />");
            File.WriteAllText(Path.Combine(sdkDir, "Sdk.targets"), "<Project />");

            // Create a project directory
            var projectDir = Path.Combine(tempDir, "project");
            Directory.CreateDirectory(projectDir);

            var resolver = new FileBasedSdkResolver();
            var result = (MockResult?)resolver.Resolve(
                new SdkReference("../TestSdk", null, null),
                new MockContext(_logger) { ProjectFileDirectory = new DirectoryInfo(projectDir) },
                new MockFactory());

            Assert.NotNull(result);
            Assert.True(result!.Success, $"Resolution should succeed. Errors: {string.Join(", ", result.Errors ?? Array.Empty<string>())}");
            Assert.Equal(sdkDir, result.Path);
            Assert.Null(result.Version);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void ItResolvesAbsolutePathSdk()
    {
        // Create a temporary SDK directory structure
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-sdk-{Guid.NewGuid()}");
        var sdkDir = Path.Combine(tempDir, "TestSdk", "Sdk");
        Directory.CreateDirectory(sdkDir);

        try
        {
            // Create Sdk.props and Sdk.targets
            File.WriteAllText(Path.Combine(sdkDir, "Sdk.props"), "<Project />");
            File.WriteAllText(Path.Combine(sdkDir, "Sdk.targets"), "<Project />");

            var resolver = new FileBasedSdkResolver();
            var result = (MockResult?)resolver.Resolve(
                new SdkReference(Path.Combine(tempDir, "TestSdk"), null, null),
                new MockContext(_logger) { ProjectFileDirectory = new DirectoryInfo(tempDir) },
                new MockFactory());

            Assert.NotNull(result);
            Assert.True(result!.Success, $"Resolution should succeed. Errors: {string.Join(", ", result.Errors ?? Array.Empty<string>())}");
            Assert.Equal(sdkDir, result.Path);
            Assert.Null(result.Version);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void ItResolvesPathWithoutSdkSubdirectory()
    {
        // Create a temporary SDK directory without Sdk subdirectory
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-sdk-{Guid.NewGuid()}");
        var sdkDir = Path.Combine(tempDir, "TestSdk");
        Directory.CreateDirectory(sdkDir);

        try
        {
            // Create Sdk.props and Sdk.targets directly in the directory
            File.WriteAllText(Path.Combine(sdkDir, "Sdk.props"), "<Project />");
            File.WriteAllText(Path.Combine(sdkDir, "Sdk.targets"), "<Project />");

            var resolver = new FileBasedSdkResolver();
            var result = (MockResult?)resolver.Resolve(
                new SdkReference("./TestSdk", null, null),
                new MockContext(_logger) { ProjectFileDirectory = new DirectoryInfo(tempDir) },
                new MockFactory());

            Assert.NotNull(result);
            Assert.True(result!.Success, $"Resolution should succeed. Errors: {string.Join(", ", result.Errors ?? Array.Empty<string>())}");
            Assert.Equal(sdkDir, result.Path);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void ItFailsWhenDirectoryDoesNotExist()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-sdk-{Guid.NewGuid()}");

        var resolver = new FileBasedSdkResolver();
        var result = (MockResult?)resolver.Resolve(
            new SdkReference("../NonExistentSdk", null, null),
            new MockContext(_logger) { ProjectFileDirectory = new DirectoryInfo(tempDir) },
            new MockFactory());

        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.NotNull(result.Errors);
        Assert.NotEmpty(result.Errors!);
        Assert.Contains("does not exist", result.Errors.First());
    }

    [Fact]
    public void ItFailsWhenSdkFilesAreMissing()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-sdk-{Guid.NewGuid()}");
        var sdkDir = Path.Combine(tempDir, "TestSdk");
        Directory.CreateDirectory(sdkDir);

        try
        {
            var resolver = new FileBasedSdkResolver();
            var result = (MockResult?)resolver.Resolve(
                new SdkReference("./TestSdk", null, null),
                new MockContext(_logger) { ProjectFileDirectory = new DirectoryInfo(tempDir) },
                new MockFactory());

            Assert.NotNull(result);
            Assert.False(result!.Success);
            Assert.NotNull(result.Errors);
            Assert.NotEmpty(result.Errors!);
            Assert.Contains("does not contain Sdk.props or Sdk.targets", result.Errors.First());
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void ItIgnoresNonPathSdkReferences()
    {
        var resolver = new FileBasedSdkResolver();
        var result = resolver.Resolve(
            new SdkReference("Microsoft.NET.Sdk", null, null),
            new MockContext(_logger) { ProjectFileDirectory = new DirectoryInfo(Path.GetTempPath()) },
            new MockFactory());

        Assert.Null(result);
    }

    private sealed class MockContext : SdkResolverContext
    {
        public new string? ProjectFilePath { get => base.ProjectFilePath; set => base.ProjectFilePath = value; }
        public new string? SolutionFilePath { get => base.SolutionFilePath; set => base.SolutionFilePath = value; }

        public DirectoryInfo ProjectFileDirectory
        {
            get => new(Path.GetDirectoryName(ProjectFilePath)!);
            set => ProjectFilePath = Path.Combine(value.FullName, "test.csproj");
        }

        public override SdkLogger Logger { get; protected set; }

        public MockContext(ITestOutputHelper? logger = null)
        {
            Logger = new MockLogger(logger);
        }
    }

    private sealed class MockFactory : SdkResultFactory
    {
        public override SdkResult IndicateFailure(IEnumerable<string> errors, IEnumerable<string>? warnings = null)
            => new MockResult(success: false, path: null, version: null, warnings: warnings, errors: errors);

        public override SdkResult IndicateSuccess(string path, string? version, IEnumerable<string>? warnings = null)
            => new MockResult(success: true, path: path, version: version, warnings: warnings);

        public override SdkResult IndicateSuccess(string path, string? version, IDictionary<string, string>? propertiesToAdd, IDictionary<string, SdkResultItem>? itemsToAdd, IEnumerable<string>? warnings = null)
            => new MockResult(success: true, path: path, version: version, warnings: warnings);
    }

    private sealed class MockResult : SdkResult
    {
        public new bool Success { get; }
        public new string? Path { get; }
        public new string? Version { get; }
        public IEnumerable<string>? Warnings { get; }
        public IEnumerable<string>? Errors { get; }

        public MockResult(bool success, string? path, string? version, IEnumerable<string>? warnings = null, IEnumerable<string>? errors = null)
        {
            Success = success;
            Path = path;
            Version = version;
            Warnings = warnings;
            Errors = errors;
        }
    }

    private sealed class MockLogger : SdkLogger
    {
        private readonly ITestOutputHelper? _logger;

        public MockLogger(ITestOutputHelper? logger)
        {
            _logger = logger;
        }

        public override void LogMessage(string message, MessageImportance messageImportance = MessageImportance.Low)
        {
            _logger?.WriteLine($"[{messageImportance}] {message}");
        }
    }
}
