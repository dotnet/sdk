// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Commands.Run.Api;

namespace Microsoft.DotNet.Cli.TestApp;

public static class RunApiTestApp
{
    public static async Task<int> Main(string[] args)
    {
        using CancellationTokenSource cancellation = new();
        using CancellationTokenSource monitorCancellation = new();
        string markerPath = args[0];
        Console.SetIn(new ObservedReader(Console.In, markerPath));
        Task monitor = Task.Run(async () =>
        {
            while (!File.Exists(markerPath + ".cancel"))
            {
                await Task.Delay(10, monitorCancellation.Token);
            }

            cancellation.Cancel();
        });

        try
        {
            return new RunApiCommand(Parser.Parse(["run-api"])).Execute(cancellation.Token);
        }
        finally
        {
            monitorCancellation.Cancel();
            try
            {
                await monitor;
            }
            catch (OperationCanceledException) when (monitorCancellation.IsCancellationRequested)
            {
            }
        }
    }

    private sealed class ObservedReader(TextReader input, string markerPath) : TextReader
    {
        public override string? ReadLine()
        {
            File.WriteAllText(markerPath, "reading");
            return input.ReadLine();
        }
    }
}
