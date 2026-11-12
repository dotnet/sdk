// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.InteropServices;
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

        if (OperatingSystem.IsLinux())
        {
            await WaitForBlockedStandardInputRead();
        }

        await cancellation.CancelAsync();
        return await execution;
    }

    private static async Task WaitForBlockedStandardInputRead()
    {
        string readSyscall = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "0",
            Architecture.Arm64 or Architecture.LoongArch64 or Architecture.RiscV64 => "63",
            Architecture.X86 or Architecture.Arm or Architecture.S390x or Architecture.Ppc64le => "3",
            _ => throw new PlatformNotSupportedException("Unknown Linux read syscall number."),
        };
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(10))
        {
            foreach (string thread in Directory.EnumerateDirectories("/proc/self/task"))
            {
                try
                {
                    string[] syscall = File.ReadAllText(Path.Combine(thread, "syscall")).Split(' ');
                    // No input is supplied, so read(fd=0) cannot complete before cancellation.
                    if (syscall.Length > 1 && syscall[0] == readSyscall && syscall[1] == "0x0")
                    {
                        return;
                    }
                }
                catch (FileNotFoundException)
                {
                    // A runtime thread can exit between enumeration and inspection.
                }
                catch (DirectoryNotFoundException)
                {
                    // A runtime thread can exit between enumeration and inspection.
                }
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("No thread entered a blocked read on standard input.");
    }
}
