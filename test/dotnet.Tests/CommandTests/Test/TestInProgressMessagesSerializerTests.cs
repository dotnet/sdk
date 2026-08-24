// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public class TestInProgressMessagesSerializerTests
{
    [TestMethod]
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

        Assert.AreEqual(original.ExecutionId, deserialized.ExecutionId);
        Assert.AreEqual(original.InstanceId, deserialized.InstanceId);
        Assert.HasCount(2, deserialized.InProgressMessages);
        Assert.AreEqual("uid-1", deserialized.InProgressMessages[0].Uid);
        Assert.AreEqual("DisplayName1", deserialized.InProgressMessages[0].DisplayName);
        Assert.AreEqual("uid-2", deserialized.InProgressMessages[1].Uid);
        Assert.AreEqual("DisplayName2", deserialized.InProgressMessages[1].DisplayName);
    }

    [TestMethod]
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

        Assert.AreEqual(original.ExecutionId, deserialized.ExecutionId);
        Assert.AreEqual(original.InstanceId, deserialized.InstanceId);
        Assert.IsEmpty(deserialized.InProgressMessages);
    }

    [TestMethod]
    public void SerializerId_IsTen()
    {
        // The IPC protocol reserves serializer IDs across SDK and MTP.
        // ID 10 is the contract — keep this assertion to prevent accidental changes.
        Assert.AreEqual(10, new TestInProgressMessagesSerializer().Id);
    }
}
