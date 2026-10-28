// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.CodeAnalysis.Tools.Utilities
{
    public static class ProcessRunner
    {
        public static async Task<int> RunAsync(
            string executable,
            IEnumerable<string> arguments,
            Action<string>? logOutput = null)
        {
            // Capture stdout only when needed; discard unused output without exposing it to the console.
            bool captureOutput = logOutput is not null;
            using SafeFileHandle nullHandle = File.OpenNullHandle();
            ProcessStartInfo startInfo = new(executable, arguments)
            {
                RedirectStandardOutput = captureOutput,
                StandardOutputHandle = captureOutput ? null : nullHandle,
                StandardErrorHandle = nullHandle,
                InheritedHandles = []
            };

            if (logOutput is null)
            {
                return (await Process.RunAsync(startInfo).ConfigureAwait(false)).ExitCode;
            }

            using Process process = Process.Start(startInfo)!;
            string output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            ProcessExitStatus status = await process.WaitForExitStatusAsync().ConfigureAwait(false);
            logOutput(output);

            return status.ExitCode;
        }
    }
}
