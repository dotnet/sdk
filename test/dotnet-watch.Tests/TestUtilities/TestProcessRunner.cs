// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch.UnitTests;

internal class TestProcessRunner()
    : ProcessRunner(processCleanupTimeout: TimeSpan.MaxValue)
{
    public Func<ProcessSpec, ILogger, ProcessLaunchResult?, int?>? RunImpl;

    public async override Task<int> RunAsync(ProcessSpec processSpec, ILogger logger, ProcessLaunchResult? launchResult, CancellationToken processTerminationToken)
    {
        var result = RunImpl?.Invoke(processSpec, logger, launchResult);
        return result ?? await base.RunAsync(processSpec, logger, launchResult, processTerminationToken);
    }
}
