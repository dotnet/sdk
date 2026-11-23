// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class AwaitableProcessTests : SdkTest
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task WaitsForOutputDeliveredAfterProcessExit(bool standardError)
    {
        string marker = standardError ? $"missing-{Guid.NewGuid():N}.dll" : "Microsoft.NETCore.App";
        var startInfo = CreateStartInfo();
        if (standardError)
        {
            startInfo.ArgumentList.Add("exec");
            startInfo.ArgumentList.Add(Path.Combine(Path.GetTempPath(), marker));
        }
        else
        {
            startInfo.ArgumentList.Add("--list-runtimes");
        }

        var outputBlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseOutput = new ManualResetEventSlim();
        var logger = new DebugTestOutputLogger(Log);
        int blocked = 0;
        logger.OnMessage += message =>
        {
            if (message.Contains(marker, StringComparison.Ordinal) && Interlocked.Exchange(ref blocked, 1) == 0)
            {
                outputBlocked.SetResult();
                releaseOutput.Wait(TimeSpan.FromSeconds(30), CancellationToken.None);
            }
        };

        await using var process = new AwaitableProcess(startInfo, logger);
        try
        {
            await outputBlocked.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
            Assert.IsTrue(process.Process.WaitForExit(30_000));
            await process.WaitForProcessExitAsync();

            var output = process.GetOutputLineAsync(line => line.Contains(marker, StringComparison.Ordinal));
            Assert.IsFalse(output.IsCompleted, "Process exit must not complete output reads before the stream callbacks finish.");

            releaseOutput.Set();
            var line = await output.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken);
            Assert.IsNotNull(line);
            line.Should().Contain(marker);
        }
        finally
        {
            releaseOutput.Set();
        }
    }

    [TestMethod]
    public async Task DrainsBufferedOutputWithoutAddingEndOfStreamLines()
    {
        var startInfo = CreateStartInfo();
        startInfo.ArgumentList.Add("--list-runtimes");
        await using var process = new AwaitableProcess(startInfo, new DebugTestOutputLogger(Log));

        Assert.IsTrue(process.Process.WaitForExit(30_000));
        await process.WaitForProcessExitAsync();

        var lines = new List<string>();
        while (await process.GetOutputLineAsync(_ => true) is { } line)
        {
            lines.Add(line);
        }

        lines.Should().NotBeEmpty().And.NotContain(string.Empty);
        lines.Should().Contain(line => line.StartsWith("Microsoft.NETCore.App ", StringComparison.Ordinal));
        Assert.IsNull(await process.GetOutputLineAsync(_ => true));
    }

    private static ProcessStartInfo CreateStartInfo() => new()
    {
        FileName = SdkTestContext.Current.ToolsetUnderTest.DotNetHostPath,
        UseShellExecute = false,
    };
}
