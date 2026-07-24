// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Pipes;
using System.Text.Json;

namespace Microsoft.DotNet.Watch.UnitTests;

internal static class PipeUtilities
{
    public static async Task<IReadOnlyList<WatchStatusEvent>> ReadStatusEventsAsync(string pipeName, int expectedEventCount, CancellationToken cancellationToken)
    {
        var lines = new List<WatchStatusEvent>();

        using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

        await pipe.WaitForConnectionAsync(cancellationToken);

        using var reader = new StreamReader(pipe, Encoding.UTF8);

        try
        {
            // Read until we have observed the expected number of events. The status events are delivered
            // over a separate pipe from the server's output, so stopping based on output (instead of the
            // event count) races with delivery and can drop trailing events.
            while (lines.Count < expectedEventCount && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line == null)
                {
                    break;
                }

                var value = JsonSerializer.Deserialize<WatchStatusEvent>(line);
                Assert.IsNotNull(value);

                lines.Add(value);
            }
        }
        catch (Exception e) when (e is IOException or OperationCanceledException)
        {
        }

        return lines;
    }
}
