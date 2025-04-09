// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Graph;

namespace Microsoft.DotNet.Watch;

internal sealed class BrowserSpecificReporter(int browserId, IReporter underlyingReporter) : IReporter
{
    private readonly string _prefix = $"[Browser #{browserId}] ";

    public bool IsVerbose
        => underlyingReporter.IsVerbose;

    public bool EnableProcessOutputReporting
        => false;

    public void ReportProcessOutput(ProjectGraphNode project, OutputLine line)
        => throw new InvalidOperationException();

    public void ReportProcessOutput(OutputLine line)
        => throw new InvalidOperationException();

    public void Report(MessageDescriptor descriptor, string prefix, object?[] args)
        => underlyingReporter.Report(descriptor, _prefix + prefix, args);
}
