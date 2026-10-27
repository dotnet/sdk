// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Test.IPC.Models;

internal sealed record CoverageSummaryMessage(
    string? SessionUid,
    string? ProducerId,
    int? ScopeLevel,
    string? ScopeName,
    int? Metric,
    string? CustomMetricName,
    long? CoveredCount,
    long? CoverableCount);

internal sealed record CoverageSummaryMessages(
    string? ExecutionId,
    string? InstanceId,
    CoverageSummaryMessage[] Summaries) : IRequest;

internal enum CoverageMetric
{
    Line = 0,
    Statement = 1,
    Branch = 2,
    Method = 3,
    Function = 4,
    Block = 5,
    Instruction = 6,
    Region = 7,
    Class = 8,
    Condition = 9,
    Complexity = 10,
    Custom = 255,
}
