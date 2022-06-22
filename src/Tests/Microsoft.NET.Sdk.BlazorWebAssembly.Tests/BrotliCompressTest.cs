// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using FluentAssertions;
using Moq;
using Xunit;
using System.Linq;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    public class BrotliCompressTest
    {
        [Fact]
        public void GeneratesResponseFileWithQuotes()
        {
            // Arrange/Act
            var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            var dummyFile = new TaskItem("Sample Path With Spaces.txt");
            dummyFile.SetMetadata("RelativePath", "Sample Path With Spaces.txt");

            var engine = new Mock<IBuildEngine>();
            
            var compressTask = new BrotliCompress
            {
                BuildEngine = engine.Object,
                // don't actually run the tool, just collect the output
                UseCommandProcessor = false,
                OutputDirectory = "out man",
                FilesToCompress = new [] { dummyFile }
            };

            var args = compressTask.GenerateResponseFileCommandsCore();

            var lines = args.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            lines.Length.Should().Be(5); // dotnet brotli, -s, arg, -o, arg
            // the only one we _know_ should be quoted is line 3 and 5, the input file path and output file path
            var inputFilePath = lines[2];
            inputFilePath.Should().StartWith("\"").And.EndWith("\"");
            var outputFilePath = lines[4];
            outputFilePath.Should().StartWith("\"").And.EndWith("\"");
        }
    }
}
