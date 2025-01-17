// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class MSBuildHandlerTests
    {
        [Fact]
        public void IsBinaryLoggerEnabled_ShouldReturnTrue_WhenBinaryLoggerArgumentIsPresent()
        {
            // Arrange
            var args = new List<string> { "--binaryLogger" };

            // Act
            var result = MSBuildHandler.IsBinaryLoggerEnabled(args, out string binLogFileName);

            // Assert
            Assert.True(result);
            Assert.Equal(CliConstants.BinLogFileName, binLogFileName);
        }

        [Fact]
        public void IsBinaryLoggerEnabled_ShouldReturnTrue_WhenBinaryLoggerArgumentWithFileNameIsPresent()
        {
            // Arrange
            var args = new List<string> { "--binaryLogger:custom.binlog" };

            // Act
            var result = MSBuildHandler.IsBinaryLoggerEnabled(args, out string binLogFileName);

            // Assert
            Assert.True(result);
            Assert.Equal("custom.binlog", binLogFileName);
        }

        [Fact]
        public void IsBinaryLoggerEnabled_ShouldReturnFalse_WhenBinaryLoggerArgumentIsNotPresent()
        {
            // Arrange
            var args = new List<string> { "--someOtherArg" };

            // Act
            var result = MSBuildHandler.IsBinaryLoggerEnabled(args, out string binLogFileName);

            // Assert
            Assert.False(result);
            Assert.Equal(string.Empty, binLogFileName);
        }

        [Fact]
        public void IsBinaryLoggerEnabled_ShouldRemoveBinaryLoggerArgumentsFromArgs()
        {
            // Arrange
            var args = new List<string> { "--binaryLogger", "--someOtherArg" };

            // Act
            var result = MSBuildHandler.IsBinaryLoggerEnabled(args, out string binLogFileName);

            // Assert
            Assert.True(result);
            Assert.Equal(CliConstants.BinLogFileName, binLogFileName);
            Assert.DoesNotContain("--binaryLogger", args);
        }

        [Fact]
        public void IsBinaryLoggerEnabled_ShouldHandleMultipleBinaryLoggerArguments()
        {
            // Arrange
            var args = new List<string> { "--binaryLogger", "--binaryLogger:custom1.binlog", "--binaryLogger:custom2.binlog" };

            // Act
            var result = MSBuildHandler.IsBinaryLoggerEnabled(args, out string binLogFileName);

            // Assert
            Assert.True(result);
            Assert.Equal("custom2.binlog", binLogFileName);
            Assert.DoesNotContain("--binaryLogger", args);
            Assert.DoesNotContain("--binaryLogger:custom1.binlog", args);
            Assert.DoesNotContain("--binaryLogger:custom2.binlog", args);
        }

        [Fact]
        public void IsBinaryLoggerEnabled_ShouldHandleInvalidBinaryLoggerArgumentFormat()
        {
            // Arrange
            var args = new List<string> { "--binaryLogger:" };

            // Act
            var result = MSBuildHandler.IsBinaryLoggerEnabled(args, out string binLogFileName);

            // Assert
            Assert.True(result);
            Assert.Equal(CliConstants.BinLogFileName, binLogFileName);
            Assert.DoesNotContain("--binaryLogger:", args);
        }

        [Fact]
        public void IsBinaryLoggerEnabled_ShouldHandleEmptyBinaryLoggerFilename()
        {
            // Arrange
            var args = new List<string> { "--binaryLogger:" };

            // Act
            var result = MSBuildHandler.IsBinaryLoggerEnabled(args, out string binLogFileName);

            // Assert
            Assert.True(result);
            Assert.Equal(CliConstants.BinLogFileName, binLogFileName);
        }
    }
}
