// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.IO.Pipes;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

internal sealed class LockFileTestProcess : IDisposable
{
    private readonly Process _process;
    private readonly NamedPipeClientStream _pipe;

    private LockFileTestProcess(Process process, NamedPipeClientStream pipe)
    {
        _process = process;
        _pipe = pipe;
    }

    public static LockFileTestProcess Start(string dotnetPath, string assemblyPath, string mode, string lockPath)
    {
        var pipeName = "scoped-lock-" + Guid.NewGuid().ToString("N");
        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
        var process = new Process { StartInfo = CreateStartInfo(dotnetPath, assemblyPath, mode, lockPath) };
        process.StartInfo.ArgumentList.Add(pipeName);
        var started = false;
        try
        {
            started = process.Start();
            Assert.IsTrue(started);
            pipe.Connect(30_000);
            return new LockFileTestProcess(process, pipe);
        }
        catch
        {
            pipe.Dispose();
            if (started)
            {
                Stop(process);
            }
            process.Dispose();
            throw;
        }
    }

    public static int Probe(string dotnetPath, string assemblyPath, string mode, string lockPath)
    {
        using var process = new Process { StartInfo = CreateStartInfo(dotnetPath, assemblyPath, mode, lockPath) };
        Assert.IsTrue(process.Start());
        try
        {
            Assert.IsTrue(process.WaitForExit(30_000), "Lock acquisition must not block.");
            return process.ExitCode;
        }
        finally
        {
            Stop(process);
        }
    }

    public void Kill() => Stop(_process);

    public void Dispose()
    {
        _pipe.Dispose();
        try
        {
            if (!_process.WaitForExit(10_000))
            {
                Stop(_process);
            }
        }
        finally
        {
            _process.Dispose();
        }
    }

    private static ProcessStartInfo CreateStartInfo(string dotnetPath, string assemblyPath, string mode, string lockPath)
    {
        var startInfo = new ProcessStartInfo(dotnetPath) { UseShellExecute = false, CreateNoWindow = true };
        startInfo.ArgumentList.Add(assemblyPath);
        startInfo.ArgumentList.Add(mode);
        startInfo.ArgumentList.Add(lockPath);
        return startInfo;
    }

    private static void Stop(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            Assert.IsTrue(process.WaitForExit(10_000), "Child process did not terminate.");
        }
    }
}