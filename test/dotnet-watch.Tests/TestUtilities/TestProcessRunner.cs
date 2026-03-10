// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch.UnitTests;

internal class TestProcessRunner()
    : ProcessRunner(processCleanupTimeout: TimeSpan.MaxValue)
{
    public Func<ProcessSpec, ILogger, ProcessLaunchResult?, int>? RunImpl;

    public override Task<int> RunAsync(ProcessSpec processSpec, ILogger logger, ProcessLaunchResult? launchResult, CancellationToken processTerminationToken)
        => Task.FromResult(RunImpl?.Invoke(processSpec, logger, launchResult) ?? throw new NotImplementedException());
}
