// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;

namespace dotnet.Tests.CommandTests.Test;

public class TestInProgressMessagesSerializerTests
{
    [Fact]
    public void RoundTrips_WithPopulatedMessages()
    {
        var serializer = new TestInProgressMessagesSerializer();
        var original = new TestInProgressMessages(
            ExecutionId: "exec-123",
            InstanceId: "inst-456",
            InProgressMessages:
            [
                new TestInProgressMessage("uid-1", "DisplayName1"),
                new TestInProgressMessage("uid-2", "DisplayName2"),
            ]);

        using var stream = new MemoryStream();
        serializer.Serialize(original, stream);
        stream.Position = 0;

        var deserialized = (TestInProgressMessages)serializer.Deserialize(stream);

        Assert.Equal(original.ExecutionId, deserialized.ExecutionId);
        Assert.Equal(original.InstanceId, deserialized.InstanceId);
        Assert.Equal(2, deserialized.InProgressMessages.Length);
        Assert.Equal("uid-1", deserialized.InProgressMessages[0].Uid);
        Assert.Equal("DisplayName1", deserialized.InProgressMessages[0].DisplayName);
        Assert.Equal("uid-2", deserialized.InProgressMessages[1].Uid);
        Assert.Equal("DisplayName2", deserialized.InProgressMessages[1].DisplayName);
    }

    [Fact]
    public void RoundTrips_WithEmptyMessagesList()
    {
        var serializer = new TestInProgressMessagesSerializer();
        var original = new TestInProgressMessages(
            ExecutionId: "exec-123",
            InstanceId: "inst-456",
            InProgressMessages: []);

        using var stream = new MemoryStream();
        serializer.Serialize(original, stream);
        stream.Position = 0;

        var deserialized = (TestInProgressMessages)serializer.Deserialize(stream);

        Assert.Equal(original.ExecutionId, deserialized.ExecutionId);
        Assert.Equal(original.InstanceId, deserialized.InstanceId);
        Assert.Empty(deserialized.InProgressMessages);
    }

    [Fact]
    public void SerializerId_IsTen()
    {
        // The IPC protocol reserves serializer IDs across SDK and MTP.
        // ID 10 is the contract — keep this assertion to prevent accidental changes.
        Assert.Equal(10, new TestInProgressMessagesSerializer().Id);
    }
}
