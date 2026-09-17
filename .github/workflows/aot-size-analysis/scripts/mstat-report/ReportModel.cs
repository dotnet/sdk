// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace MstatReport;

internal sealed class SizeReport
{
    public required string Name { get; init; }
    public required string Source { get; init; }
    public required Version MstatVersion { get; init; }
    public required long AttributedSize { get; init; }
    public required SizeNode Root { get; init; }
    public required int DeduplicatedMethodCount { get; init; }
    public RetentionReport Retention { get; set; } = RetentionReport.Empty;
}

internal sealed class SizeNode
{
    public required string Name { get; init; }
    public required string Kind { get; init; }
    public required long Size { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long ExclusiveSize { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SizeNode>? Children { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? DeduplicatedWith { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<int>? RetentionIds { get; set; }

    [JsonIgnore]
    public string? MstatLabel { get; init; }
}

internal sealed class RetentionReport
{
    public static RetentionReport Empty { get; } = new()
    {
        Source = "",
        Nodes = [],
        DirectEdges = [],
        RootParents = []
    };

    public required string Source { get; init; }
    public required Dictionary<int, string> Nodes { get; init; }
    public required List<RetentionEdge> DirectEdges { get; init; }
    public required Dictionary<int, RetentionParent> RootParents { get; init; }
}

internal sealed record RetentionEdge(int Source, int Target, string Reason);

internal sealed record RetentionParent(int Source, string Reason);
