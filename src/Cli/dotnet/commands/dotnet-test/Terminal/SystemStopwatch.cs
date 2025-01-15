// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace Microsoft.Testing.Platform.Helpers;

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
