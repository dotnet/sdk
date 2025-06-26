// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Logging;

namespace Microsoft.DotNet.Watch;

internal sealed class BuildLogger : ConsoleLogger
{
    private readonly List<OutputLine> _messages = [];

    public BuildLogger()
    {
        WriteHandler = Write;
    }

    public IReadOnlyList<OutputLine> Messages
        => _messages;

    public void Clear()
        => _messages.Clear();

    private void Write(string message)
        => _messages.Add(new OutputLine(message.TrimEnd('\r', '\n'), IsError: false));
}
