// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public class DisplayMessageSerializerTests
{
    [TestMethod]
    public void RoundTrips_WithAllFieldsPopulated()
    {
        var serializer = new DisplayMessageSerializer();
        var original = new DisplayMessage(
            ExecutionId: "exec-123",
            InstanceId: "inst-456",
            Level: DisplayMessageLevels.Warning,
            Text: "A warning from the host");

        using var stream = new MemoryStream();
        serializer.Serialize(original, stream);
        stream.Position = 0;

        var deserialized = (DisplayMessage)serializer.Deserialize(stream);

        Assert.AreEqual(original.ExecutionId, deserialized.ExecutionId);
        Assert.AreEqual(original.InstanceId, deserialized.InstanceId);
        Assert.AreEqual(original.Level, deserialized.Level);
        Assert.AreEqual(original.Text, deserialized.Text);
    }

    [TestMethod]
    public void RoundTrips_OmitsNullStringFields_ButAlwaysWritesLevel()
    {
        var serializer = new DisplayMessageSerializer();
        var original = new DisplayMessage(
            ExecutionId: null,
            InstanceId: null,
            Level: DisplayMessageLevels.Error,
            Text: null);

        using var stream = new MemoryStream();
        serializer.Serialize(original, stream);
        stream.Position = 0;

        var deserialized = (DisplayMessage)serializer.Deserialize(stream);

        Assert.IsNull(deserialized.ExecutionId);
        Assert.IsNull(deserialized.InstanceId);
        Assert.AreEqual(DisplayMessageLevels.Error, deserialized.Level);
        Assert.IsNull(deserialized.Text);
    }

    [TestMethod]
    public void RoundTrips_InformationLevel()
    {
        var serializer = new DisplayMessageSerializer();
        var original = new DisplayMessage(
            ExecutionId: "exec-1",
            InstanceId: "inst-1",
            Level: DisplayMessageLevels.Information,
            Text: "info");

        using var stream = new MemoryStream();
        serializer.Serialize(original, stream);
        stream.Position = 0;

        var deserialized = (DisplayMessage)serializer.Deserialize(stream);

        Assert.AreEqual(DisplayMessageLevels.Information, deserialized.Level);
        Assert.AreEqual("info", deserialized.Text);
    }

    [TestMethod]
    public void SerializerId_IsTwelve()
    {
        // The IPC protocol reserves serializer IDs across SDK and MTP.
        // ID 12 is the contract - keep this assertion to prevent accidental changes.
        Assert.AreEqual(12, new DisplayMessageSerializer().Id);
    }
}
