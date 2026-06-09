// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.Cli.Commands.Test.Terminal;

internal sealed class SystemStopwatch : IStopwatch
{
    private readonly Stopwatch _stopwatch = new();

    public TimeSpan Elapsed => _stopwatch.Elapsed;

    public void Start() => _stopwatch.Start();

    public void Stop() => _stopwatch.Stop();

    public static IStopwatch StartNew()
    {
        SystemStopwatch wallClockStopwatch = new();
        wallClockStopwatch.Start();

        return wallClockStopwatch;
    }
}
