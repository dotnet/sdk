// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.DotNet.Cli.Commands.Test.IPC;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public class TestResultMessagesSerializerTests
{
    [TestMethod]
    public void RoundTrip_FailedTest_PreservesExpectedAndActual()
    {
        var original = new TestResultMessages(
            ExecutionId: "exec-1",
            InstanceId: "inst-2",
            SuccessfulTestMessages: [],
            FailedTestMessages:
            [
                new FailedTestResultMessage(
                    Uid: "uid-1",
                    DisplayName: "My Failing Test",
                    State: 4,
                    Duration: 123,
                    Reason: "Assert.AreEqual failed",
                    Exceptions: [new ExceptionMessage("boom", "System.Exception", "at Foo()")],
                    StandardOutput: "stdout",
                    ErrorOutput: "stderr",
                    SessionUid: "session-1",
                    Expected: "expected value",
                    Actual: "actual value"),
            ]);

        TestResultMessages roundTripped = SerializeAndDeserialize(original);

        roundTripped.ExecutionId.Should().Be("exec-1");
        roundTripped.InstanceId.Should().Be("inst-2");
        roundTripped.FailedTestMessages.Should().HaveCount(1);
        var test = roundTripped.FailedTestMessages[0];
        test.Uid.Should().Be("uid-1");
        test.DisplayName.Should().Be("My Failing Test");
        test.State.Should().Be(4);
        test.Duration.Should().Be(123);
        test.Reason.Should().Be("Assert.AreEqual failed");
        test.Exceptions.Should().HaveCount(1);
        test.StandardOutput.Should().Be("stdout");
        test.ErrorOutput.Should().Be("stderr");
        test.SessionUid.Should().Be("session-1");
        test.Expected.Should().Be("expected value");
        test.Actual.Should().Be("actual value");
    }

    [TestMethod]
    public void RoundTrip_FailedTest_NullExpectedAndActual_AreOmitted()
    {
        // Mimics a host that does not populate the assertion diff fields (e.g. a non-assertion failure
        // or an older testfx build): Expected/Actual are absent on the wire and deserialize back to null.
        var original = new TestResultMessages(
            ExecutionId: null,
            InstanceId: null,
            SuccessfulTestMessages: [],
            FailedTestMessages:
            [
                new FailedTestResultMessage(
                    Uid: "uid-1",
                    DisplayName: "My Failing Test",
                    State: 4,
                    Duration: null,
                    Reason: null,
                    Exceptions: [],
                    StandardOutput: null,
                    ErrorOutput: null,
                    SessionUid: null,
                    Expected: null,
                    Actual: null),
            ]);

        byte[] bytes = Serialize(original);

        TestResultMessages roundTripped = Deserialize(bytes);
        roundTripped.FailedTestMessages.Should().HaveCount(1);
        var test = roundTripped.FailedTestMessages[0];
        test.Uid.Should().Be("uid-1");
        test.DisplayName.Should().Be("My Failing Test");
        test.Expected.Should().BeNull();
        test.Actual.Should().BeNull();

        // Walk to the single failed-test entry and confirm the Expected/Actual field ids never appear on the wire.
        using var ms = new MemoryStream(bytes);
        using var reader = new BinaryReader(ms);

        _ = reader.ReadUInt16(); // outer field count
        _ = reader.ReadUInt16(); // FailedTestMessageList field id
        _ = reader.ReadInt32();  // list payload size
        int listLength = reader.ReadInt32();
        listLength.Should().Be(1);

        ushort innerFieldCount = reader.ReadUInt16();
        for (int i = 0; i < innerFieldCount; i++)
        {
            ushort fieldId = reader.ReadUInt16();
            fieldId.Should().NotBe(FailedTestResultMessageFieldsId.Expected);
            fieldId.Should().NotBe(FailedTestResultMessageFieldsId.Actual);
            int fieldSize = reader.ReadInt32();
            reader.ReadBytes(fieldSize);
        }
    }

    private static byte[] Serialize(TestResultMessages message)
    {
        var serializer = new TestResultMessagesSerializer();
        using var stream = new MemoryStream();
        serializer.Serialize(message, stream);
        return stream.ToArray();
    }

    private static TestResultMessages Deserialize(byte[] bytes)
    {
        var serializer = new TestResultMessagesSerializer();
        using var stream = new MemoryStream(bytes);
        return (TestResultMessages)serializer.Deserialize(stream);
    }

    private static TestResultMessages SerializeAndDeserialize(TestResultMessages message)
        => Deserialize(Serialize(message));
}
