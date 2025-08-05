// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Graph;

namespace Microsoft.DotNet.Watch;

internal sealed class ProjectSpecificReporter(ProjectGraphNode node, IReporter underlyingReporter) : IReporter
{
    private readonly string _projectDisplayName = node.GetDisplayName();

    public bool IsVerbose
        => underlyingReporter.IsVerbose;

    public void ReportProcessOutput(OutputLine line)
        => underlyingReporter.ReportProcessOutput(
            underlyingReporter.PrefixProcessOutput ? line with { Content = $"[{_projectDisplayName}] {line.Content}" } : line);

    public void Report(MessageDescriptor descriptor, string prefix, object?[] args)
        => underlyingReporter.Report(descriptor, $"[{_projectDisplayName}] {prefix}", args);
}
