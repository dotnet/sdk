// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Json;
using Microsoft.DotNet.Cli.TestApp;

namespace Microsoft.DotNet.Cli.Run.Tests;

[TestClass]
public class RunApiCancellationTests : SdkTest
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(30);

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Linux)]
    public async Task CancellationInterruptsAnOpenStandardInputPipe()
        => await VerifyStandardInputCancellation();

    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public async Task CancellationInterruptsABlockedLinuxStandardInputRead()
        => await VerifyStandardInputCancellation();

    private async Task VerifyStandardInputCancellation()
    {
        using Process child = StartChild(cancelPendingRead: true);
        Task<string> standardOutput = child.StandardOutput.ReadToEndAsync(TestContext.CancellationToken);
        Task<string> standardError = child.StandardError.ReadToEndAsync(TestContext.CancellationToken);
        try
        {
            await child.WaitForExitAsync(TestContext.CancellationToken).WaitAsync(s_timeout, TestContext.CancellationToken);
            child.ExitCode.Should().Be(0);
            (await standardOutput).Should().BeEmpty();
            (await standardError).Should().BeEmpty();
        }
        finally
        {
            StopChild(child);
        }
    }

    [TestMethod]
    [DataRow("\n")]
    [DataRow("\r\n")]
    [DataRow("\r")]
    public async Task InputLinesBlankLinesAndEndOfFileRetainTheirResponses(string newline)
    {
        using Process child = StartChild();
        Task<string> standardOutput = child.StandardOutput.ReadToEndAsync(TestContext.CancellationToken);
        Task<string> standardError = child.StandardError.ReadToEndAsync(TestContext.CancellationToken);
        try
        {
            await child.StandardInput.WriteAsync(
                $" {newline}{newline}{{\"$type\":\"Unknown1\"}}{newline}{{\"$type\":\"Unknown2\"}}");
            child.StandardInput.Close();
            await child.WaitForExitAsync(TestContext.CancellationToken).WaitAsync(s_timeout, TestContext.CancellationToken);
            child.ExitCode.Should().Be(0);
            string output = await standardOutput;
            string[] responses = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            responses.Should().HaveCount(2);
            for (int i = 0; i < responses.Length; i++)
            {
                using JsonDocument response = JsonDocument.Parse(responses[i]);
                response.RootElement.GetProperty("$type").GetString().Should().Be("Error");
                response.RootElement.GetProperty("Version").GetInt32().Should().Be(1);
                response.RootElement.GetProperty("Message").GetString().Should().Contain($"Unknown{i + 1}");
            }

            (await standardError).Should().BeEmpty();
        }
        finally
        {
            StopChild(child);
        }
    }

    [TestMethod]
    public async Task EmptyEndOfFileExitsWithoutAResponse()
    {
        using Process child = StartChild();
        Task<string> standardOutput = child.StandardOutput.ReadToEndAsync(TestContext.CancellationToken);
        Task<string> standardError = child.StandardError.ReadToEndAsync(TestContext.CancellationToken);
        try
        {
            child.StandardInput.Close();
            await child.WaitForExitAsync(TestContext.CancellationToken).WaitAsync(s_timeout, TestContext.CancellationToken);
            child.ExitCode.Should().Be(0);
            (await standardOutput).Should().BeEmpty();
            (await standardError).Should().BeEmpty();
        }
        finally
        {
            StopChild(child);
        }
    }

    private static Process StartChild(bool cancelPendingRead = false)
    {
        string assemblyPath = typeof(RunApiCancellationTests).Assembly.Location;
        ProcessStartInfo startInfo = new(SdkTestContext.Current.ToolsetUnderTest.DotNetHostPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        if (cancelPendingRead)
        {
            startInfo.ArgumentList.Add("exec");
            startInfo.ArgumentList.Add("--runtimeconfig");
            startInfo.ArgumentList.Add(Path.ChangeExtension(assemblyPath, ".runtimeconfig.json"));
            startInfo.ArgumentList.Add("--depsfile");
            startInfo.ArgumentList.Add(Path.ChangeExtension(assemblyPath, ".deps.json"));
            startInfo.ArgumentList.Add(typeof(RunApiTestApp).Assembly.Location);
        }
        else
        {
            startInfo.ArgumentList.Add("run-api");
        }
        Process child = new() { StartInfo = startInfo };
        child.Start();
        return child;
    }

    private static void StopChild(Process child)
    {
        if (!child.HasExited)
        {
            child.Kill(entireProcessTree: true);
            child.WaitForExit(s_timeout).Should().BeTrue();
        }
    }
}
