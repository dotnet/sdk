// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public class FileArtifactMessagesSerializerTests
{
    [TestMethod]
    public void RoundTrip_PreservesArtifactKind()
    {
        var original = new FileArtifactMessages(
            ExecutionId: "exec-1",
            InstanceId: "instance-1",
            FileArtifacts:
            [
                new FileArtifactMessage(
                    FullPath: "/repo/TestResults/results.trx",
                    DisplayName: "results.trx",
                    Description: "Test results",
                    TestUid: null,
                    TestDisplayName: null,
                    SessionUid: "session-1",
                    Kind: "trx"),
            ]);

        var serializer = new FileArtifactMessagesSerializer();
        using var stream = new MemoryStream();
        serializer.Serialize(original, stream);
        stream.Position = 0;

        var roundTripped = (FileArtifactMessages)serializer.Deserialize(stream);

        roundTripped.FileArtifacts.Should().ContainSingle();
        roundTripped.FileArtifacts[0].Kind.Should().Be("trx");
    }
}
