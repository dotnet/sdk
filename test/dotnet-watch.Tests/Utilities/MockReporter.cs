// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.Build.Graph;
using Microsoft.DotNet.Watcher.Internal;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools;

internal class MockReporter : IReporter
{
    public readonly List<string> Messages = [];

    public bool EnableProcessOutputReporting => false;

    public void ReportProcessOutput(OutputLine line)
    => throw new InvalidOperationException();

    public void ReportProcessOutput(ProjectGraphNode project, OutputLine line)
        => throw new InvalidOperationException();

    public void Report(MessageDescriptor descriptor, string prefix, object?[] args)
    {
        if (descriptor.TryGetMessage(prefix, args, out var message))
        {
            Messages.Add($"{ToString(descriptor.Severity)} {descriptor.Emoji} {message}");
        }
    }

    private static string ToString(MessageSeverity severity)
        => severity switch
        {
            MessageSeverity.Verbose => "verbose",
            MessageSeverity.Output => "output",
            MessageSeverity.Warning => "warning",
            MessageSeverity.Error => "error",
            _ => throw new InvalidOperationException()
        };
}
