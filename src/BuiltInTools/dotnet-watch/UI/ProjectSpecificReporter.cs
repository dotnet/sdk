// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Graph;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal sealed class ProjectSpecificReporter : IReporter
{
    private readonly string _projectDisplayName;
    private readonly IReporter _underlyingReporter;

    public readonly ILogger ClientLogger;
    public readonly ILogger AgentLogger;

    public ProjectSpecificReporter(ProjectGraphNode node, IReporter underlyingReporter, ILoggerFactory loggerFactory)
    {
        _underlyingReporter = underlyingReporter;
        _projectDisplayName = node.GetDisplayName();
        ClientLogger = loggerFactory.CreateLogger(HotReloadDotNetWatcher.ClientLogComponentName, _projectDisplayName);
        AgentLogger = loggerFactory.CreateLogger(HotReloadDotNetWatcher.AgentLogComponentName, _projectDisplayName);
    }

    public bool IsVerbose
        => _underlyingReporter.IsVerbose;

    public void ReportProcessOutput(OutputLine line)
        => _underlyingReporter.ReportProcessOutput(
            _underlyingReporter.PrefixProcessOutput ? line with { Content = $"[{_projectDisplayName}] {line.Content}" } : line);

    public void Report(MessageDescriptor descriptor, string prefix, object?[] args)
        => _underlyingReporter.Report(descriptor, $"[{_projectDisplayName}] {prefix}", args);
}
