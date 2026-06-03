// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.DotNet.Cli.Commands.Test.IPC;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;

namespace dotnet.Tests.CommandTests.Test;

public class DiscoveredTestMessagesSerializerTests
{
    [Fact]
    public void RoundTrip_AllFieldsPopulated_PreservesValues()
    {
        var original = new DiscoveredTestMessages(
            ExecutionId: "exec-1",
            InstanceId: "inst-2",
            DiscoveredMessages:
            [
                new DiscoveredTestMessage(
                    Uid: "uid-1",
                    DisplayName: "My Test 1",
                    FilePath: "C:/repo/MyTests.cs",
                    LineNumber: 42,
                    Namespace: "My.Namespace",
                    TypeName: "MyTests",
                    MethodName: "DoesSomething",
                    ParameterTypeFullNames: ["System.Int32", "System.String"],
                    Traits: [new TraitMessage("Category", "Smoke"), new TraitMessage("Owner", "team-x")]),
            ]);

        DiscoveredTestMessages roundTripped = SerializeAndDeserialize(original);

        roundTripped.ExecutionId.Should().Be("exec-1");
        roundTripped.InstanceId.Should().Be("inst-2");
        roundTripped.DiscoveredMessages.Should().HaveCount(1);
        var test = roundTripped.DiscoveredMessages[0];
        test.Uid.Should().Be("uid-1");
        test.DisplayName.Should().Be("My Test 1");
        test.FilePath.Should().Be("C:/repo/MyTests.cs");
        test.LineNumber.Should().Be(42);
        test.Namespace.Should().Be("My.Namespace");
        test.TypeName.Should().Be("MyTests");
        test.MethodName.Should().Be("DoesSomething");
        test.ParameterTypeFullNames.Should().Equal("System.Int32", "System.String");
        test.Traits.Should().HaveCount(2);
        test.Traits[0].Key.Should().Be("Category");
        test.Traits[0].Value.Should().Be("Smoke");
        test.Traits[1].Key.Should().Be("Owner");
        test.Traits[1].Value.Should().Be("team-x");
    }

    [Fact]
    public void RoundTrip_OnlyUidAndDisplayName_BackwardCompatibility()
    {
        // Mimics a legacy MTP producing only Uid/DisplayName: optional fields are absent on the wire,
        // and the SDK must successfully deserialize, defaulting arrays to empty and other fields to null.
        var original = new DiscoveredTestMessages(
            ExecutionId: "exec-1",
            InstanceId: null,
            DiscoveredMessages:
            [
                new DiscoveredTestMessage(
                    Uid: "uid-1",
                    DisplayName: "My Test",
                    FilePath: null,
                    LineNumber: null,
                    Namespace: null,
                    TypeName: null,
                    MethodName: null,
                    ParameterTypeFullNames: [],
                    Traits: []),
            ]);

        DiscoveredTestMessages roundTripped = SerializeAndDeserialize(original);

        roundTripped.ExecutionId.Should().Be("exec-1");
        roundTripped.InstanceId.Should().BeNull();
        roundTripped.DiscoveredMessages.Should().HaveCount(1);
        var test = roundTripped.DiscoveredMessages[0];
        test.Uid.Should().Be("uid-1");
        test.DisplayName.Should().Be("My Test");
        test.FilePath.Should().BeNull();
        test.LineNumber.Should().BeNull();
        test.Namespace.Should().BeNull();
        test.TypeName.Should().BeNull();
        test.MethodName.Should().BeNull();
        test.ParameterTypeFullNames.Should().BeEmpty();
        test.Traits.Should().BeEmpty();
    }

    [Fact]
    public void Serialize_LineNumber_UsesFourBytes()
    {
        // The wire format for LineNumber is a 4-byte signed integer. This protects against
        // accidentally widening LineNumber to a long (8 bytes), which would break compatibility
        // with the testfx (MTP) producer.
        var message = new DiscoveredTestMessages(
            ExecutionId: null,
            InstanceId: null,
            DiscoveredMessages:
            [
                new DiscoveredTestMessage(
                    Uid: null,
                    DisplayName: null,
                    FilePath: null,
                    LineNumber: 7,
                    Namespace: null,
                    TypeName: null,
                    MethodName: null,
                    ParameterTypeFullNames: [],
                    Traits: []),
            ]);

        byte[] bytes = Serialize(message);

        // Walk to the LineNumber field within the single inner test entry and assert that its
        // declared size on the wire is 4 bytes.
        using var ms = new MemoryStream(bytes);
        using var reader = new BinaryReader(ms);

        ushort outerFieldCount = reader.ReadUInt16();
        outerFieldCount.Should().Be(1, "only the DiscoveredTestMessageList field is populated");

        ushort outerFieldId = reader.ReadUInt16();
        outerFieldId.Should().Be(DiscoveredTestMessagesFieldsId.DiscoveredTestMessageList);
        _ = reader.ReadInt32(); // payload size of the list
        int listLength = reader.ReadInt32();
        listLength.Should().Be(1);

        ushort innerFieldCount = reader.ReadUInt16();
        innerFieldCount.Should().Be(1, "only the LineNumber field is populated on the inner test");

        ushort innerFieldId = reader.ReadUInt16();
        innerFieldId.Should().Be(DiscoveredTestMessageFieldsId.LineNumber);

        int lineNumberFieldSize = reader.ReadInt32();
        lineNumberFieldSize.Should().Be(sizeof(int), "LineNumber must be serialized as 4 bytes");

        int lineNumberValue = reader.ReadInt32();
        lineNumberValue.Should().Be(7);
    }

    [Fact]
    public void Serialize_EmptyArrays_AreOmittedFromWire()
    {
        // Empty Traits and empty ParameterTypeFullNames should be omitted entirely (no field id, no size).
        var message = new DiscoveredTestMessages(
            ExecutionId: null,
            InstanceId: null,
            DiscoveredMessages:
            [
                new DiscoveredTestMessage(
                    Uid: "u",
                    DisplayName: null,
                    FilePath: null,
                    LineNumber: null,
                    Namespace: null,
                    TypeName: null,
                    MethodName: null,
                    ParameterTypeFullNames: [],
                    Traits: []),
            ]);

        byte[] bytes = Serialize(message);

        using var ms = new MemoryStream(bytes);
        using var reader = new BinaryReader(ms);

        _ = reader.ReadUInt16(); // outer field count
        _ = reader.ReadUInt16(); // DiscoveredTestMessageList field id
        _ = reader.ReadInt32();  // list payload size
        _ = reader.ReadInt32();  // list length
        ushort innerFieldCount = reader.ReadUInt16();
        innerFieldCount.Should().Be(1, "only Uid is populated; empty Traits and empty ParameterTypeFullNames must not appear on the wire");
    }

    private static byte[] Serialize(DiscoveredTestMessages message)
    {
        var serializer = new DiscoveredTestMessagesSerializer();
        using var stream = new MemoryStream();
        serializer.Serialize(message, stream);
        return stream.ToArray();
    }

    private static DiscoveredTestMessages SerializeAndDeserialize(DiscoveredTestMessages message)
    {
        byte[] bytes = Serialize(message);
        var serializer = new DiscoveredTestMessagesSerializer();
        using var stream = new MemoryStream(bytes);
        return (DiscoveredTestMessages)serializer.Deserialize(stream);
    }
}
