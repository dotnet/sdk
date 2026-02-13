// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher;

internal sealed class MessagePrefixingReporter(string additionalPrefix, IReporter underlyingReporter) : IReporter
{
    public bool IsVerbose
        => underlyingReporter.IsVerbose;

    public bool ReportProcessOutput
        => underlyingReporter.ReportProcessOutput;

    public void ProcessOutput(string projectPath, string data)
        => underlyingReporter.ProcessOutput(projectPath, data);

    public void Report(MessageDescriptor descriptor, string prefix, object?[] args)
        => underlyingReporter.Report(descriptor, additionalPrefix + prefix, args);
}
