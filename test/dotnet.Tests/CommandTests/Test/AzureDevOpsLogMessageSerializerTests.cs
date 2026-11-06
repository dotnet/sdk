// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public class AzureDevOpsLogMessageSerializerTests
{
    [TestMethod]
    public void RoundTrips_WithAllFieldsPopulated()
    {
        var serializer = new AzureDevOpsLogMessageSerializer();
        var original = new AzureDevOpsLogMessage(
            ExecutionId: "exec-123",
            InstanceId: "inst-456",
            LogText: "##[group]My group");

        using var stream = new MemoryStream();
        serializer.Serialize(original, stream);
        stream.Position = 0;

        var deserialized = (AzureDevOpsLogMessage)serializer.Deserialize(stream);

        Assert.AreEqual(original.ExecutionId, deserialized.ExecutionId);
        Assert.AreEqual(original.InstanceId, deserialized.InstanceId);
        Assert.AreEqual(original.LogText, deserialized.LogText);
    }

    [TestMethod]
    public void RoundTrips_OmitsNullFields()
    {
        var serializer = new AzureDevOpsLogMessageSerializer();
        var original = new AzureDevOpsLogMessage(
            ExecutionId: null,
            InstanceId: null,
            LogText: "##[endgroup]");

        using var stream = new MemoryStream();
        serializer.Serialize(original, stream);
        stream.Position = 0;

        var deserialized = (AzureDevOpsLogMessage)serializer.Deserialize(stream);

        Assert.IsNull(deserialized.ExecutionId);
        Assert.IsNull(deserialized.InstanceId);
        Assert.AreEqual("##[endgroup]", deserialized.LogText);
    }

    [TestMethod]
    public void SerializerId_IsEleven()
    {
        // The IPC protocol reserves serializer IDs across SDK and MTP.
        // ID 11 is the contract - keep this assertion to prevent accidental changes.
        Assert.AreEqual(11, new AzureDevOpsLogMessageSerializer().Id);
    }
}
