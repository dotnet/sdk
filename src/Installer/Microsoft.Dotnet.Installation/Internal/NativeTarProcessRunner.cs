// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Diagnostics;

namespace Microsoft.Dotnet.Installation.Internal;

internal interface INativeTarProcessRunner
{
    NativeTarProcessResult Run(string executable, IReadOnlyList<string> arguments);
}

internal sealed record NativeTarProcessResult(int? ExitCode, string StandardError, Exception? StartFailure = null);

internal sealed class NativeTarProcessRunner : INativeTarProcessRunner
{
    private const int MaximumStandardErrorLength = 16 * 1024;

    public NativeTarProcessResult Run(string executable, IReadOnlyList<string> arguments)
    {
        using var standardInput = File.OpenNullHandle();
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputHandle = standardInput,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        // ArgumentList preserves each argument boundary without constructing a command line
        // whose quoting rules would vary with paths and the selected executable.
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            var result = Process.RunAndCaptureText(startInfo);
            Console.Write(result.StandardOutput);
            return new NativeTarProcessResult(result.ExitStatus.ExitCode,
                result.StandardError[..Math.Min(result.StandardError.Length, MaximumStandardErrorLength)]);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return new NativeTarProcessResult(ExitCode: null, StandardError: string.Empty, StartFailure: ex);
        }
    }
}
