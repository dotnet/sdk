// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Xml;

namespace MstatReport;

internal static class RetentionGraphReader
{
    public static RetentionReport Read(string path, SizeNode root)
    {
        var sizeNodesByLabel = new Dictionary<string, List<SizeNode>>(StringComparer.Ordinal);
        CollectLabels(root, sizeNodesByLabel);

        GraphNodes graphNodes = MapGraphNodes(path, sizeNodesByLabel);
        GraphEdges graphEdges = ReadEdges(path, graphNodes);
        int[] parents = FindShortestRootPaths(graphNodes.Present, graphEdges);

        var neededIds = new HashSet<int>(graphNodes.MappedIds);
        foreach (RetentionEdge edge in graphEdges.DirectEdges)
        {
            neededIds.Add(edge.Source);
        }

        foreach (int mappedId in graphNodes.MappedIds)
        {
            int cursor = mappedId;
            while (cursor >= 0)
            {
                neededIds.Add(cursor);
                cursor = parents[cursor];
            }
        }

        (Dictionary<int, string> names, Dictionary<int, RetentionParent> rootParents) =
            ReadNeededDetails(path, neededIds, parents);
        return new RetentionReport
        {
            Source = path,
            Nodes = names,
            DirectEdges = graphEdges.DirectEdges,
            RootParents = rootParents
        };
    }

    private static GraphNodes MapGraphNodes(
        string path,
        Dictionary<string, List<SizeNode>> sizeNodesByLabel)
    {
        bool[] present = new bool[1024];
        var mappedIds = new HashSet<int>();
        using XmlReader reader = CreateReader(path);
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "Node")
            {
                continue;
            }

            int id = ReadId(reader, "Id");
            EnsureLength(ref present, id);
            present[id] = true;
            string label = RequiredAttribute(reader, "Label");
            if (!sizeNodesByLabel.TryGetValue(label, out List<SizeNode>? sizeNodes))
            {
                continue;
            }

            mappedIds.Add(id);
            foreach (SizeNode sizeNode in sizeNodes)
            {
                sizeNode.RetentionIds ??= [];
                sizeNode.RetentionIds.Add(id);
            }
        }

        return new GraphNodes(present, mappedIds);
    }

    private static GraphEdges ReadEdges(string path, GraphNodes nodes)
    {
        int[] heads = new int[nodes.Present.Length];
        Array.Fill(heads, -1);
        int[] indegrees = new int[nodes.Present.Length];
        var targets = new List<int>();
        var nextEdges = new List<int>();
        var directEdges = new List<RetentionEdge>();

        using XmlReader reader = CreateReader(path);
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "Link")
            {
                continue;
            }

            int source = ReadId(reader, "Source");
            int target = ReadId(reader, "Target");
            if ((uint)source >= (uint)nodes.Present.Length ||
                (uint)target >= (uint)nodes.Present.Length ||
                !nodes.Present[source] ||
                !nodes.Present[target])
            {
                throw new InvalidDataException(
                    $"DGML link {source} -> {target} refers to an undeclared node.");
            }

            int edgeIndex = targets.Count;
            targets.Add(target);
            nextEdges.Add(heads[source]);
            heads[source] = edgeIndex;
            indegrees[target] = checked(indegrees[target] + 1);

            if (nodes.MappedIds.Contains(target))
            {
                directEdges.Add(new RetentionEdge(
                    source,
                    target,
                    reader.GetAttribute("Reason") ?? ""));
            }
        }

        return new GraphEdges(heads, indegrees, targets, nextEdges, directEdges);
    }

    private static int[] FindShortestRootPaths(bool[] present, GraphEdges graph)
    {
        int[] parents = new int[present.Length];
        Array.Fill(parents, -2);
        int[] queue = new int[present.Count(static value => value)];
        int read = 0;
        int write = 0;
        for (int id = 0; id < present.Length; id++)
        {
            if (present[id] && graph.Indegrees[id] == 0)
            {
                parents[id] = -1;
                queue[write++] = id;
            }
        }

        while (read < write)
        {
            int source = queue[read++];
            for (int edge = graph.Heads[source]; edge >= 0; edge = graph.NextEdges[edge])
            {
                int target = graph.Targets[edge];
                if (parents[target] == -2)
                {
                    parents[target] = source;
                    queue[write++] = target;
                }
            }
        }

        return parents;
    }

    private static (
        Dictionary<int, string> Names,
        Dictionary<int, RetentionParent> RootParents) ReadNeededDetails(
        string path,
        HashSet<int> neededIds,
        int[] parents)
    {
        var names = new Dictionary<int, string>();
        var rootParents = new Dictionary<int, RetentionParent>();
        using XmlReader reader = CreateReader(path);
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (reader.LocalName == "Node")
            {
                int id = ReadId(reader, "Id");
                if (neededIds.Contains(id))
                {
                    names[id] = RequiredAttribute(reader, "Label");
                }
            }
            else if (reader.LocalName == "Link")
            {
                int source = ReadId(reader, "Source");
                int target = ReadId(reader, "Target");
                if ((uint)target < (uint)parents.Length &&
                    neededIds.Contains(target) &&
                    parents[target] == source &&
                    !rootParents.ContainsKey(target))
                {
                    rootParents.Add(
                        target,
                        new RetentionParent(source, reader.GetAttribute("Reason") ?? ""));
                }
            }
        }

        return (names, rootParents);
    }

    private static void CollectLabels(
        SizeNode node,
        Dictionary<string, List<SizeNode>> nodesByLabel)
    {
        if (node.MstatLabel is not null)
        {
            if (!nodesByLabel.TryGetValue(node.MstatLabel, out List<SizeNode>? nodes))
            {
                nodes = [];
                nodesByLabel.Add(node.MstatLabel, nodes);
            }

            nodes.Add(node);
        }

        foreach (SizeNode child in node.Children ?? [])
        {
            CollectLabels(child, nodesByLabel);
        }
    }

    private static XmlReader CreateReader(string path) =>
        XmlReader.Create(
            path,
            new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                IgnoreWhitespace = true,
                CloseInput = true
            });

    private static int ReadId(XmlReader reader, string attribute)
    {
        string text = RequiredAttribute(reader, attribute);
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int id) ||
            id < 0)
        {
            throw new InvalidDataException(
                $"DGML {reader.LocalName} has invalid numeric {attribute} '{text}'.");
        }

        return id;
    }

    private static string RequiredAttribute(XmlReader reader, string name) =>
        reader.GetAttribute(name)
        ?? throw new InvalidDataException($"DGML {reader.LocalName} has no {name} attribute.");

    private static void EnsureLength(ref bool[] values, int index)
    {
        if (index < values.Length)
        {
            return;
        }

        int length = values.Length;
        while (length <= index)
        {
            length = checked(length * 2);
        }

        Array.Resize(ref values, length);
    }

    private sealed record GraphNodes(bool[] Present, HashSet<int> MappedIds);

    private sealed record GraphEdges(
        int[] Heads,
        int[] Indegrees,
        List<int> Targets,
        List<int> NextEdges,
        List<RetentionEdge> DirectEdges);
}
