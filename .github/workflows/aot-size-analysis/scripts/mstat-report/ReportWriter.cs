// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MstatReport;

internal static class ReportWriter
{
    private static readonly UTF8Encoding s_utf8WithoutBom = new(false);
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public static void Write(
        SizeReport report,
        string htmlPath,
        string genericCsvPath,
        string singleCsvPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(htmlPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(genericCsvPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(singleCsvPath)!);

        string json = JsonSerializer.Serialize(report, s_jsonOptions);
        string template = ReadTemplate();
        File.WriteAllText(
            htmlPath,
            template.Replace("__MSTAT_REPORT_DATA__", json, StringComparison.Ordinal),
            s_utf8WithoutBom);

        List<NodeLocation> nodes = [];
        Collect(report.Root, [], nodes);
        WriteGenericCsv(genericCsvPath, nodes);
        WriteSingleDependencyCsv(singleCsvPath, nodes, report.Retention);
    }

    private static string ReadTemplate()
    {
        Assembly assembly = typeof(ReportWriter).Assembly;
        string resourceName = assembly.GetManifestResourceNames()
            .Single(static name => name.EndsWith(".index.template.html", StringComparison.Ordinal));
        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("The embedded HTML template is unavailable.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static void Collect(
        SizeNode node,
        List<string> parentPath,
        List<NodeLocation> destination)
    {
        var path = new List<string>(parentPath.Count + 1);
        path.AddRange(parentPath);
        path.Add(node.Name);
        destination.Add(new NodeLocation(node, path));
        foreach (SizeNode child in node.Children ?? [])
        {
            Collect(child, path, destination);
        }
    }

    private static void WriteGenericCsv(string path, List<NodeLocation> nodes)
    {
        using var writer = new StreamWriter(path, false, s_utf8WithoutBom);
        writer.WriteLine("Kind,SizeBytes,ExclusiveBytes,RetentionGraphId,Assembly,Namespace,Type,Member,Path");
        foreach (NodeLocation location in nodes
            .Where(static item => item.Node.Kind is "type instantiation" or "method instantiation")
            .OrderByDescending(static item => item.Node.Size)
            .ThenBy(static item => item.PathText, StringComparer.Ordinal))
        {
            WriteCsvRow(
                writer,
                location.Node.Kind,
                location.Node.Size.ToString(System.Globalization.CultureInfo.InvariantCulture),
                location.Node.ExclusiveSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
                location.RetentionIds,
                location.SegmentAfter("assembly"),
                location.SegmentAfter("namespace"),
                location.SegmentAfter("type"),
                location.Node.Kind == "method instantiation" ? location.Node.Name : "",
                location.PathText);
        }
    }

    private static void WriteSingleDependencyCsv(
        string path,
        List<NodeLocation> nodes,
        RetentionReport retention)
    {
        Dictionary<int, List<RetentionEdge>> incoming = retention.DirectEdges
            .GroupBy(static edge => edge.Target)
            .ToDictionary(static group => group.Key, static group => group.ToList());

        using var writer = new StreamWriter(path, false, s_utf8WithoutBom);
        writer.WriteLine(
            "ExclusiveBytes,TotalBytes,Kind,Name,RetentionGraphId,SourceGraphId,Source,Reason,Path");
        foreach ((NodeLocation location, RetentionEdge edge) in nodes
            .Where(item =>
                item.Node.RetentionIds is not null &&
                item.Node.RetentionIds
                    .SelectMany(id => incoming.GetValueOrDefault(id) ?? [])
                    .Take(2)
                    .Count() == 1)
            .Select(item => (
                item,
                item.Node.RetentionIds!
                    .SelectMany(id => incoming.GetValueOrDefault(id) ?? [])
                    .Single()))
            .OrderByDescending(static item => item.item.Node.ExclusiveSize)
            .ThenBy(static item => item.item.PathText, StringComparer.Ordinal))
        {
            WriteCsvRow(
                writer,
                location.Node.ExclusiveSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
                location.Node.Size.ToString(System.Globalization.CultureInfo.InvariantCulture),
                location.Node.Kind,
                location.Node.Name,
                location.RetentionIds,
                edge.Source.ToString(System.Globalization.CultureInfo.InvariantCulture),
                retention.Nodes.GetValueOrDefault(edge.Source, $"Node {edge.Source}"),
                edge.Reason,
                location.PathText);
        }
    }

    private static void WriteCsvRow(TextWriter writer, params object[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }

            string value = values[i]?.ToString() ?? "";
            writer.Write('"');
            writer.Write(value.Replace("\"", "\"\"", StringComparison.Ordinal));
            writer.Write('"');
        }

        writer.WriteLine();
    }

    private sealed record NodeLocation(SizeNode Node, List<string> Path)
    {
        public string PathText => string.Join(" \u203a ", Path);

        public string RetentionIds => Node.RetentionIds is null
            ? ""
            : string.Join(';', Node.RetentionIds);

        public string SegmentAfter(string kind) => kind switch
        {
            "assembly" => Path.Count > 1 ? Path[1] : "",
            "namespace" => Path.Count > 2 ? Path[2] : "",
            "type" => Path.Count > 3 ? Path[3] : "",
            _ => ""
        };
    }
}
