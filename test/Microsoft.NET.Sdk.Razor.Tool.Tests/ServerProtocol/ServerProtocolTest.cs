// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.NET.Sdk.Razor.Tool.Tests
{
    [TestClass]
    public class ServerProtocolTest
    {
        [TestMethod]
        public async Task ServerResponse_WriteRead_RoundtripsProperly()
        {
            // Arrange
            var response = new CompletedServerResponse(42, utf8output: false, output: "a string", error: "an error");
            var memoryStream = new MemoryStream();

            // Act
            await response.WriteAsync(memoryStream, CancellationToken.None);

            // Assert
            Assert.IsGreaterThan(0, memoryStream.Position);
            memoryStream.Position = 0;
            var result = (CompletedServerResponse)await ServerResponse.ReadAsync(memoryStream, CancellationToken.None);
            result.ReturnCode.Should().Be(42);
            result.Utf8Output.Should().Be(false);
            result.Output.Should().Be("a string");
            result.ErrorOutput.Should().Be("an error");
        }

        [TestMethod]
        public async Task ServerRequest_WriteRead_RoundtripsProperly()
        {
            // Arrange
            var request = new ServerRequest(
                ServerProtocol.ProtocolVersion,
                ImmutableArray.Create(
                    new RequestArgument(RequestArgument.ArgumentId.CurrentDirectory, argumentIndex: 0, value: "directory"),
                    new RequestArgument(RequestArgument.ArgumentId.CommandLineArgument, argumentIndex: 1, value: "file")));
            var memoryStream = new MemoryStream();

            // Act
            await request.WriteAsync(memoryStream, CancellationToken.None);

            // Assert
            Assert.IsGreaterThan(0, memoryStream.Position);
            memoryStream.Position = 0;
            var read = await ServerRequest.ReadAsync(memoryStream, CancellationToken.None);

            read.ProtocolVersion.Should().Be(ServerProtocol.ProtocolVersion);
            read.Arguments.Count.Should().Be(2);
            read.Arguments[0].Id.Should().Be(RequestArgument.ArgumentId.CurrentDirectory);
            read.Arguments[0].ArgumentIndex.Should().Be(0);
            read.Arguments[0].Value.Should().Be("directory");
            read.Arguments[1].Id.Should().Be(RequestArgument.ArgumentId.CommandLineArgument);
            read.Arguments[1].ArgumentIndex.Should().Be(1);
            read.Arguments[1].Value.Should().Be("file");
        }

        [TestMethod]
        public void CreateShutdown_CreatesCorrectShutdownRequest()
        {
            // Arrange & Act
            var request = ServerRequest.CreateShutdown();

            // Assert
            Assert.HasCount(2, request.Arguments);

            var argument1 = request.Arguments[0];
            argument1.Id.Should().Be(RequestArgument.ArgumentId.Shutdown);
            argument1.ArgumentIndex.Should().Be(0);
            argument1.Value.Should().Be("");

            var argument2 = request.Arguments[1];
            Assert.AreEqual(RequestArgument.ArgumentId.CommandLineArgument, argument2.Id);
            Assert.AreEqual(1, argument2.ArgumentIndex);
            Assert.AreEqual("shutdown", argument2.Value);
        }

        [TestMethod]
        public async Task ShutdownRequest_WriteRead_RoundtripsProperly()
        {
            // Arrange
            var memoryStream = new MemoryStream();
            var request = ServerRequest.CreateShutdown();

            // Act
            await request.WriteAsync(memoryStream, CancellationToken.None);

            // Assert
            memoryStream.Position = 0;
            var read = await ServerRequest.ReadAsync(memoryStream, CancellationToken.None);

            var argument1 = request.Arguments[0];
            Assert.AreEqual(RequestArgument.ArgumentId.Shutdown, argument1.Id);
            Assert.AreEqual(0, argument1.ArgumentIndex);
            Assert.AreEqual("", argument1.Value);

            var argument2 = request.Arguments[1];
            Assert.AreEqual(RequestArgument.ArgumentId.CommandLineArgument, argument2.Id);
            Assert.AreEqual(1, argument2.ArgumentIndex);
            Assert.AreEqual("shutdown", argument2.Value);
        }

        [TestMethod]
        public async Task ShutdownResponse_WriteRead_RoundtripsProperly()
        {
            // Arrange & Act 1
            var memoryStream = new MemoryStream();
            var response = new ShutdownServerResponse(42);

            // Assert 1
            Assert.AreEqual(ServerResponse.ResponseType.Shutdown, response.Type);

            // Act 2
            await response.WriteAsync(memoryStream, CancellationToken.None);

            // Assert 2
            memoryStream.Position = 0;
            var read = await ServerResponse.ReadAsync(memoryStream, CancellationToken.None);

            read.Type.Should().Be(ServerResponse.ResponseType.Shutdown);
            var typed = (ShutdownServerResponse)read;
            typed.ServerProcessId.Should().Be(42);
        }
    }
}
