// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Commands.Run.Api;

namespace Microsoft.DotNet.Cli.TestApp;

public static class RunApiTestApp
{
    public static async Task<int> Main()
    {
        using CancellationTokenSource cancellation = new();
        Task<int> execution = new RunApiCommand(Parser.Parse(["run-api"])).ExecuteAsync(cancellation.Token);
        if (execution.IsCompleted)
        {
            throw new InvalidOperationException("Run API must be waiting for input before cancellation.");
        }

        await cancellation.CancelAsync();
        return await execution;
    }
}
