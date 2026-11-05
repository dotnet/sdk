// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;

namespace Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;

internal sealed class CoverageSummaryMessagesSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => CoverageSummaryMessagesFieldsId.MessagesSerializerId;

    public object Deserialize(Stream stream)
    {
        string? executionId = null;
        string? instanceId = null;
        CoverageSummaryMessage[] summaries = [];

        ReadFields(stream, (fieldId, fieldSize) =>
        {
            if (TryReadExecutionScopedField(stream, fieldId, fieldSize, ref executionId, ref instanceId))
            {
                return true;
            }

            if (fieldId == CoverageSummaryMessagesFieldsId.Summaries)
            {
                summaries = ReadFieldPayload(stream, fieldSize, ReadSummaries);
                return true;
            }

            return false;
        });

        return new CoverageSummaryMessages(executionId, instanceId, summaries);
    }

    public void Serialize(object objectToSerialize, Stream stream)
    {
        var message = (CoverageSummaryMessages)objectToSerialize;
        WriteExecutionScopedHeader(stream, message.ExecutionId, message.InstanceId, (ushort)(IsNullOrEmpty(message.Summaries) ? 0 : 1));
        WriteSummaries(stream, CoverageSummaryMessagesFieldsId.Summaries, message.Summaries);
    }

    internal static CoverageSummaryMessage[] ReadSummaries(Stream stream)
    {
        int length = ReadCollectionLength(stream, sizeof(ushort));
        var summaries = new CoverageSummaryMessage[length];
        for (int i = 0; i < length; i++)
        {
            string? sessionUid = null;
            string? producerId = null;
            int? scopeLevel = null;
            string? scopeName = null;
            int? metric = null;
            string? customMetricName = null;
            long? coveredCount = null;
            long? coverableCount = null;

            ReadFields(stream, (fieldId, fieldSize) =>
            {
                switch (fieldId)
                {
                    case CoverageSummaryMessageFieldsId.SessionUid:
                        sessionUid = ReadStringValue(stream, fieldSize);
                        return true;
                    case CoverageSummaryMessageFieldsId.ProducerId:
                        producerId = ReadStringValue(stream, fieldSize);
                        return true;
                    case CoverageSummaryMessageFieldsId.ScopeLevel:
                        scopeLevel = ReadFieldPayload(stream, fieldSize, ReadInt);
                        return true;
                    case CoverageSummaryMessageFieldsId.ScopeName:
                        scopeName = ReadStringValue(stream, fieldSize);
                        return true;
                    case CoverageSummaryMessageFieldsId.Metric:
                        metric = ReadFieldPayload(stream, fieldSize, ReadInt);
                        return true;
                    case CoverageSummaryMessageFieldsId.CustomMetricName:
                        customMetricName = ReadStringValue(stream, fieldSize);
                        return true;
                    case CoverageSummaryMessageFieldsId.CoveredCount:
                        coveredCount = ReadFieldPayload(stream, fieldSize, ReadLong);
                        return true;
                    case CoverageSummaryMessageFieldsId.CoverableCount:
                        coverableCount = ReadFieldPayload(stream, fieldSize, ReadLong);
                        return true;
                    default:
                        return false;
                }
            });

            summaries[i] = new CoverageSummaryMessage(
                sessionUid, producerId, scopeLevel, scopeName, metric, customMetricName, coveredCount, coverableCount);
        }

        return summaries;
    }

    internal static void WriteSummaries(Stream stream, ushort fieldId, CoverageSummaryMessage[]? summaries)
        => WriteListPayload(stream, fieldId, summaries, static (s, summary) =>
        {
            WriteUShort(s, (ushort)(
                (summary.SessionUid is null ? 0 : 1)
                + (summary.ProducerId is null ? 0 : 1)
                + (summary.ScopeLevel is null ? 0 : 1)
                + (summary.ScopeName is null ? 0 : 1)
                + (summary.Metric is null ? 0 : 1)
                + (summary.CustomMetricName is null ? 0 : 1)
                + (summary.CoveredCount is null ? 0 : 1)
                + (summary.CoverableCount is null ? 0 : 1)));

            WriteField(s, CoverageSummaryMessageFieldsId.SessionUid, summary.SessionUid);
            WriteField(s, CoverageSummaryMessageFieldsId.ProducerId, summary.ProducerId);
            WriteField(s, CoverageSummaryMessageFieldsId.ScopeLevel, summary.ScopeLevel);
            WriteField(s, CoverageSummaryMessageFieldsId.ScopeName, summary.ScopeName);
            WriteField(s, CoverageSummaryMessageFieldsId.Metric, summary.Metric);
            WriteField(s, CoverageSummaryMessageFieldsId.CustomMetricName, summary.CustomMetricName);
            WriteField(s, CoverageSummaryMessageFieldsId.CoveredCount, summary.CoveredCount);
            WriteField(s, CoverageSummaryMessageFieldsId.CoverableCount, summary.CoverableCount);
        });
}
