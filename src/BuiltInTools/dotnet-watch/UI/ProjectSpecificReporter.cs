// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Graph;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal sealed class ProjectSpecificReporter : IReporter
{
    private readonly IReporter _underlyingReporter;

    public readonly string ProjectDisplayName;
    public readonly ILogger ClientLogger;
    public readonly ILogger AgentLogger;

    public ProjectSpecificReporter(ProjectGraphNode node, IReporter underlyingReporter, ILoggerFactory loggerFactory)
    {
        _underlyingReporter = underlyingReporter;
        ProjectDisplayName = node.GetDisplayName();
        ClientLogger = loggerFactory.CreateLogger(HotReloadDotNetWatcher.ClientLogComponentName, ProjectDisplayName);
        AgentLogger = loggerFactory.CreateLogger(HotReloadDotNetWatcher.AgentLogComponentName, ProjectDisplayName);
    }

    public bool IsVerbose
        => _underlyingReporter.IsVerbose;

    public void Report(MessageDescriptor descriptor, string prefix, object?[] args)
        => _underlyingReporter.Report(descriptor, $"[{ProjectDisplayName}] {prefix}", args);
}
