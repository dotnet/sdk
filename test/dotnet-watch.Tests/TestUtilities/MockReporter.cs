// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests;

internal class MockReporter : IReporter
{
    public readonly List<string> Messages = [];

    public void ReportProcessOutput(OutputLine line)
    {
    }

    public void Report(MessageDescriptor descriptor, string prefix, object?[] args)
    {
        if (descriptor.Severity != MessageSeverity.None)
        {
            var message = descriptor.GetMessage(prefix, args);
            Messages.Add($"{ToString(descriptor.Severity)} {descriptor.Emoji.ToDisplay()} {message}");
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
