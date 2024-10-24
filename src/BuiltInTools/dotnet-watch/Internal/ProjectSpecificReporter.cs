// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Graph;
using Microsoft.DotNet.Watcher.Internal;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher;

internal sealed class ProjectSpecificReporter(ProjectGraphNode node, IReporter underlyingReporter) : IReporter
{
    private readonly string _projectDisplayName = node.GetDisplayName();

    public bool IsVerbose
        => underlyingReporter.IsVerbose;

    public bool EnableProcessOutputReporting
        => underlyingReporter.EnableProcessOutputReporting;

    public void ReportProcessOutput(ProjectGraphNode project, OutputLine line)
        => underlyingReporter.ReportProcessOutput(project, line);

    public void ReportProcessOutput(OutputLine line)
        => ReportProcessOutput(node, line);

    public void Report(MessageDescriptor descriptor, string prefix, object?[] args)
        => underlyingReporter.Report(descriptor, $"[{_projectDisplayName}] {prefix}", args);
}
