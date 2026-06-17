// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;

namespace dotnet.Tests.CommandTests.Test;

public class TestHostOutputDeviceMessageSerializerTests
{
    [Fact]
    public void RoundTrip_AllFieldsPopulated_PreservesValues()
    {
        var original = new TestHostOutputDeviceMessage(
            ExecutionId: "exec-123",
            InstanceId: "inst-456",
            Text: "Hello from the test host");

        TestHostOutputDeviceMessage roundTripped = SerializeAndDeserialize(original);

        roundTripped.ExecutionId.Should().Be("exec-123");
        roundTripped.InstanceId.Should().Be("inst-456");
        roundTripped.Text.Should().Be("Hello from the test host");
    }

    [Fact]
    public void RoundTrip_NullsAndEmptyStrings_PreservesValues()
    {
        var original = new TestHostOutputDeviceMessage(
            ExecutionId: null,
            InstanceId: string.Empty,
            Text: string.Empty);

        TestHostOutputDeviceMessage roundTripped = SerializeAndDeserialize(original);

        roundTripped.ExecutionId.Should().BeNull();
        roundTripped.InstanceId.Should().BeEmpty();
        roundTripped.Text.Should().BeEmpty();
    }

    [Fact]
    public void SerializerId_IsEleven()
    {
        // The IPC protocol reserves serializer IDs across SDK and MTP.
        // ID 11 is the contract — keep this assertion to prevent accidental changes.
        new TestHostOutputDeviceMessageSerializer().Id.Should().Be(11);
    }

    private static TestHostOutputDeviceMessage SerializeAndDeserialize(TestHostOutputDeviceMessage message)
    {
        var serializer = new TestHostOutputDeviceMessageSerializer();
        using var stream = new MemoryStream();
        serializer.Serialize(message, stream);
        stream.Position = 0;

        return (TestHostOutputDeviceMessage)serializer.Deserialize(stream);
    }
}
