// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public class ServerControlMessageSerializerTests
{
    [TestMethod]
    public void ServerControlMessageRoundTrips()
    {
        var serializer = new ServerControlMessageSerializer();
        var message = new ServerControlMessage(ServerControlKinds.CancelSession);
        using var stream = new MemoryStream();

        serializer.Serialize(message, stream);
        stream.Position = 0;

        serializer.Deserialize(stream).Should().Be(message);
    }

    [TestMethod]
    public void SerializerIdsMatchTestFxContract()
    {
        new WaitForServerControlRequestSerializer().Id.Should().Be(13);
        new ServerControlMessageSerializer().Id.Should().Be(14);
        new InvocationSetupCompletedSerializer().Id.Should().Be(16);
        new ModuleTestsCompletedSerializer().Id.Should().Be(17);
        new InvocationCleanupCompletedSerializer().Id.Should().Be(18);
    }

    [TestMethod]
    public void RunInvocationCleanupRoundTripsWithAggregateResult()
    {
        var serializer = new ServerControlMessageSerializer();
        var message = new ServerControlMessage(ServerControlKinds.RunInvocationCleanup, AggregateExitCode: 2, IsCanceled: true);
        using var stream = new MemoryStream();

        serializer.Serialize(message, stream);
        stream.Position = 0;

        serializer.Deserialize(stream).Should().Be(message);
    }

    [TestMethod]
    public void InvocationSetupCompletedRoundTripsOutputsIncludingRemovalAndEmptyValues()
    {
        var serializer = new InvocationSetupCompletedSerializer();
        var message = new InvocationSetupCompleted(
            "exec",
            "inst",
            Succeeded: true,
            ErrorMessage: null,
            [new("SET", true, "value"), new("EMPTY", true, string.Empty), new("REMOVED", false, null)]);
        using var stream = new MemoryStream();

        serializer.Serialize(message, stream);
        stream.Position = 0;
        var result = (InvocationSetupCompleted)serializer.Deserialize(stream);

        result.ExecutionId.Should().Be("exec");
        result.InstanceId.Should().Be("inst");
        result.Succeeded.Should().BeTrue();
        result.Outputs.Should().Equal(message.Outputs);
        result.ToString().Should().NotContain("value");
    }

    [TestMethod]
    public void ModuleTestsAndCleanupCompletedRoundTrip()
    {
        var testsCompleted = new ModuleTestsCompleted("exec", "inst", ExitCode: 2);
        var cleanupCompleted = new InvocationCleanupCompleted("exec", "inst", Succeeded: false, ErrorMessage: "failed");
        using var testsStream = new MemoryStream();
        using var cleanupStream = new MemoryStream();

        new ModuleTestsCompletedSerializer().Serialize(testsCompleted, testsStream);
        new InvocationCleanupCompletedSerializer().Serialize(cleanupCompleted, cleanupStream);
        testsStream.Position = 0;
        cleanupStream.Position = 0;

        new ModuleTestsCompletedSerializer().Deserialize(testsStream).Should().Be(testsCompleted);
        new InvocationCleanupCompletedSerializer().Deserialize(cleanupStream).Should().Be(cleanupCompleted);
    }

    [TestMethod]
    public void WaitForServerControlRequestHasNoPayload()
    {
        var serializer = new WaitForServerControlRequestSerializer();
        using var stream = new MemoryStream();

        serializer.Serialize(WaitForServerControlRequest.CachedInstance, stream);

        stream.Length.Should().Be(0);
        serializer.Deserialize(stream).Should().BeSameAs(WaitForServerControlRequest.CachedInstance);
    }

    [TestMethod]
    public void PrimitiveByteAndBoolReadsThrowAtEndOfStream()
    {
        using var byteStream = new MemoryStream();
        using var boolStream = new MemoryStream();

        Assert.ThrowsExactly<EndOfStreamException>(() => BaseSerializerProbe.ReadByteValue(byteStream));
        Assert.ThrowsExactly<EndOfStreamException>(() => BaseSerializerProbe.ReadBoolValue(boolStream));
    }

    [TestMethod]
    public void UnsupportedPrimitiveSizeThrowsInvalidOperationException()
    {
        using var stream = new MemoryStream();

        Assert.ThrowsExactly<InvalidOperationException>(() => BaseSerializerProbe.WritePrimitiveSize<decimal>(stream));
    }

    private sealed class BaseSerializerProbe : BaseSerializer
    {
        public static byte ReadByteValue(Stream stream) => ReadByte(stream);

        public static bool ReadBoolValue(Stream stream) => ReadBool(stream);

        public static void WritePrimitiveSize<T>(Stream stream)
            where T : struct
            => WriteSize<T>(stream);
    }
}
