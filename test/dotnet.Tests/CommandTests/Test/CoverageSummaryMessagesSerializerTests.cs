// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Commands.Test.IPC;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public class CoverageSummaryMessagesSerializerTests
{
    [TestMethod]
    public void FieldIds_MatchNegotiatedProtocolContract()
    {
        CoverageSummaryMessagesFieldsId.MessagesSerializerId.Should().Be(15);
        HandshakeMessagePropertyNames.CoverageSummaryVersion.Should().Be(20);
        FileArtifactMessageFieldsId.CoverageSummary.Should().Be(9);
        CoverageSummaryMessageFieldsId.SessionUid.Should().Be(1);
        CoverageSummaryMessageFieldsId.ProducerId.Should().Be(2);
        CoverageSummaryMessageFieldsId.ScopeLevel.Should().Be(3);
        CoverageSummaryMessageFieldsId.ScopeName.Should().Be(4);
        CoverageSummaryMessageFieldsId.Metric.Should().Be(5);
        CoverageSummaryMessageFieldsId.CustomMetricName.Should().Be(6);
        CoverageSummaryMessageFieldsId.CoveredCount.Should().Be(7);
        CoverageSummaryMessageFieldsId.CoverableCount.Should().Be(8);
    }

    [TestMethod]
    public void RoundTrip_PreservesIdentityScopesCustomMetricsAndLongCounts()
    {
        var original = new CoverageSummaryMessages(
            "execution", "instance",
            [
                new("session", "collector", 0, null, (int)CoverageMetric.Line, null, 184, 186),
                new("session", "collector", 1, "library.dll", (int)CoverageMetric.Custom, "MC/DC", long.MaxValue - 1, long.MaxValue),
                new("another-session", "another-collector", 0, null, (int)CoverageMetric.Branch, null, 0, 0),
            ]);
        var serializer = new CoverageSummaryMessagesSerializer();
        using var stream = new MemoryStream();

        serializer.Serialize(original, stream);
        stream.Position = 0;
        var actual = (CoverageSummaryMessages)serializer.Deserialize(stream);

        actual.ExecutionId.Should().Be(original.ExecutionId);
        actual.InstanceId.Should().Be(original.InstanceId);
        actual.Summaries.Should().Equal(original.Summaries);
    }

    [TestMethod]
    public void RoundTrip_MissingCountsRemainMissingRatherThanZero()
    {
        var original = new CoverageSummaryMessages(null, null, [new(null, null, null, null, null, null, null, null)]);
        var serializer = new CoverageSummaryMessagesSerializer();
        using var stream = new MemoryStream();

        serializer.Serialize(original, stream);
        stream.Position = 0;
        var actual = (CoverageSummaryMessages)serializer.Deserialize(stream);

        actual.Summaries.Should().Equal(original.Summaries);
        actual.Summaries[0].CoveredCount.Should().BeNull();
        actual.Summaries[0].CoverableCount.Should().BeNull();
    }

    [TestMethod]
    public void Deserialize_SkipsUnknownFields()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((ushort)2);
            writer.Write((ushort)100);
            writer.Write(sizeof(int));
            writer.Write(42);
            writer.Write((ushort)CoverageSummaryMessagesFieldsId.Summaries);
            writer.Write(14);
            writer.Write(1);
            writer.Write((ushort)1);
            writer.Write((ushort)100);
            writer.Write(sizeof(ushort));
            writer.Write((ushort)42);
        }

        stream.Position = 0;
        var actual = (CoverageSummaryMessages)new CoverageSummaryMessagesSerializer().Deserialize(stream);

        actual.Summaries.Should().ContainSingle();
        actual.Summaries[0].CoveredCount.Should().BeNull();
    }

    [TestMethod]
    public void Deserialize_RejectsCountLargerThanBoundedPayload()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((ushort)1);
            writer.Write((ushort)CoverageSummaryMessagesFieldsId.Summaries);
            writer.Write(sizeof(int));
            writer.Write(int.MaxValue);
        }

        stream.Position = 0;
        Action deserialize = () => new CoverageSummaryMessagesSerializer().Deserialize(stream);

        deserialize.Should().Throw<InvalidDataException>();
    }

    [TestMethod]
    public void Deserialize_RejectsTruncatedNumericField()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((ushort)1);
            writer.Write((ushort)CoverageSummaryMessagesFieldsId.Summaries);
            writer.Write(16);
            writer.Write(1);
            writer.Write((ushort)1);
            writer.Write((ushort)CoverageSummaryMessageFieldsId.CoveredCount);
            writer.Write(sizeof(int));
            writer.Write(184);
        }

        stream.Position = 0;
        Action deserialize = () => new CoverageSummaryMessagesSerializer().Deserialize(stream);

        deserialize.Should().Throw<EndOfStreamException>();
    }
}
