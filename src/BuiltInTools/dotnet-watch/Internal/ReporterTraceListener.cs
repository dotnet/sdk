// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.Watch;

internal class ReporterTraceListener(IReporter reporter, string emoji) : TraceListener
{
    // unused
    public override void Write(string? message)
        => WriteLine(message);

    public override void WriteLine(string? message)
    {
        if (message != null)
        {
            reporter.Verbose(message, emoji);
        }
    }
}
