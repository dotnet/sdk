// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.IO.Compression;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests;

public class CompressionTasksTest
{
    [Fact]
    public void BrotliCompress_ForwardsCompressionOptionsToTool()
    {
        var task = new TestBrotliCompress
        {
            BuildEngine = Mock.Of<IBuildEngine>(),
            CompressionLevel = "Fastest",
            FilesToCompress = [],
            MaxDegreeOfParallelism = 4,
            ToolAssembly = "tool.dll",
        };

        var responseFile = task.ResponseFileCommands();

        responseFile.Should().Contain("-c");
        responseFile.Should().Contain("Fastest");
        responseFile.Should().Contain("--max-degree-of-parallelism");
        responseFile.Should().Contain("4");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void BrotliCompress_RejectsInvalidMaxDegreeOfParallelism(int maxDegreeOfParallelism)
    {
        var task = new TestBrotliCompress
        {
            BuildEngine = Mock.Of<IBuildEngine>(),
            FilesToCompress = [],
            MaxDegreeOfParallelism = maxDegreeOfParallelism,
            ToolAssembly = "tool.dll",
        };

        task.Validate().Should().BeFalse();
    }

    [Fact]
    public void GZipCompress_CompressesWithConfiguredMaxDegreeOfParallelism()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), nameof(CompressionTasksTest), Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(testDirectory);
            var inputPath = Path.Combine(testDirectory, "input.txt");
            var outputPath = Path.Combine(testDirectory, "compressed", "input.txt.gz");
            File.WriteAllText(inputPath, "compressed content");

            var fileToCompress = new TaskItem(outputPath);
            fileToCompress.SetMetadata("RelatedAsset", inputPath);
            var task = new GZipCompress
            {
                BuildEngine = Mock.Of<IBuildEngine>(),
                FilesToCompress = [fileToCompress],
                MaxDegreeOfParallelism = 1,
            };

            task.Execute().Should().BeTrue();
            using var output = File.OpenRead(outputPath);
            using var gzip = new GZipStream(output, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            reader.ReadToEnd().Should().Be("compressed content");
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void GZipCompress_RejectsInvalidMaxDegreeOfParallelism(int maxDegreeOfParallelism)
    {
        var task = new GZipCompress
        {
            BuildEngine = Mock.Of<IBuildEngine>(),
            FilesToCompress = [],
            MaxDegreeOfParallelism = maxDegreeOfParallelism,
        };

        task.Execute().Should().BeFalse();
    }

    private sealed class TestBrotliCompress : BrotliCompress
    {
        public string ResponseFileCommands() => GenerateResponseFileCommands();

        public bool Validate() => ValidateParameters();
    }
}
