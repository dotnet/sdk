// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Text;
using Microsoft.DotNet.Cli.Commands.Test.IPC;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public class FileArtifactMessagesSerializerTests
{
    [TestMethod]
    public void InputArtifactPathsFieldId_MatchesProtocolContract()
    {
        FileArtifactMessageFieldsId.InputArtifactPaths.Should().Be(8);
    }

    [TestMethod]
    public void RoundTrip_PreservesArtifactKindAndInputPaths()
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
                    Kind: "trx",
                    InputArtifactPaths: ["/repo/TestResults/first.trx", "/repo/TestResults/second.trx"]),
            ]);

        var serializer = new FileArtifactMessagesSerializer();
        using var stream = new MemoryStream();
        serializer.Serialize(original, stream);
        stream.Position = 0;

        var roundTripped = (FileArtifactMessages)serializer.Deserialize(stream);

        roundTripped.FileArtifacts.Should().ContainSingle();
        roundTripped.FileArtifacts[0].Kind.Should().Be("trx");
        roundTripped.FileArtifacts[0].InputArtifactPaths.Should().Equal(
            "/repo/TestResults/first.trx",
            "/repo/TestResults/second.trx");
    }

    [TestMethod]
    public void Deserialize_RejectsInputArtifactPathCountLargerThanItsField()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((ushort)1);
            writer.Write((ushort)FileArtifactMessagesFieldsId.FileArtifactMessageList);
            writer.Write(16);
            writer.Write(1);
            writer.Write((ushort)1);
            writer.Write((ushort)FileArtifactMessageFieldsId.InputArtifactPaths);
            writer.Write(sizeof(int));
            writer.Write(int.MaxValue);
        }

        stream.Position = 0;
        var serializer = new FileArtifactMessagesSerializer();

        Action deserialize = () => serializer.Deserialize(stream);

        deserialize.Should().Throw<InvalidDataException>();
    }

    [TestMethod]
    public void RoundTrip_PreservesMergedCoverageCountsAndProvenance()
    {
        CoverageSummaryMessage[] summary =
        [
            new(null, "collector.postprocessor", 0, null, (int)CoverageMetric.Line, null, 184, 186),
        ];
        var original = new FileArtifactMessages(
            "merge-execution", "merge-instance",
            [new("merged.cobertura.xml", null, null, null, null, null, "microsoft.codecoverage", ["first.coverage", "second.coverage"], summary)]);
        var serializer = new FileArtifactMessagesSerializer();
        using var stream = new MemoryStream();

        serializer.Serialize(original, stream);
        stream.Position = 0;
        var actual = (FileArtifactMessages)serializer.Deserialize(stream);

        actual.FileArtifacts[0].CoverageSummary.Should().Equal(summary);
        actual.FileArtifacts[0].InputArtifactPaths.Should().Equal("first.coverage", "second.coverage");
    }

    [TestMethod]
    public void RoundTrip_LegacyArtifactHasNoCoverageSummary()
    {
        var original = new FileArtifactMessages(
            "execution", "instance", [new("report.coverage", null, null, null, null, null, "microsoft.codecoverage")]);
        var serializer = new FileArtifactMessagesSerializer();
        using var stream = new MemoryStream();

        serializer.Serialize(original, stream);
        stream.Position = 0;
        var actual = (FileArtifactMessages)serializer.Deserialize(stream);

        actual.FileArtifacts[0].CoverageSummary.Should().BeNull();
    }
}
