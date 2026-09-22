// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.Cli.Utils;

[TestClass]
public class CommandTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ExecuteWithCanceledTokenTerminatesProcess()
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = OperatingSystem.IsWindows() ? "ping.exe" : "/bin/sleep",
            Arguments = OperatingSystem.IsWindows()
                ? "-n 600 -w 1000 127.0.0.1"
                : "600",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        using Process process = new() { StartInfo = startInfo };
        using var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var command = new Command(process);

        Task<CommandResult> execution = Task.Run(() => command.Execute(cancellationSource.Token));
        try
        {
            Assert.IsTrue(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            return process.Id > 0;
                        }
                        catch (InvalidOperationException)
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "The helper process did not start.");
            await cancellationSource.CancelAsync();

            await Assert.ThrowsExactlyAsync<OperationCanceledException>(
                () => execution.WaitAsync(TestContext.CancellationToken));
            Assert.IsTrue(process.HasExited);
        }
        finally
        {
            await cancellationSource.CancelAsync();
        }
    }
}
