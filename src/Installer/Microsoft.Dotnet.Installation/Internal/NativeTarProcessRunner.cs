// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace Microsoft.Dotnet.Installation.Internal;

internal interface INativeTarProcessRunner
{
    NativeTarProcessResult Run(string executable, IReadOnlyList<string> arguments);
}

internal sealed record NativeTarProcessResult(int? ExitCode, string StandardError, Exception? StartFailure = null);

internal sealed class NativeTarProcessRunner : INativeTarProcessRunner
{
    private const int MaximumStandardErrorLength = 16 * 1024;
    private const int StandardErrorReadBufferLength = 1024;

    public NativeTarProcessResult Run(string executable, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardError = true,
        };

        // ArgumentList preserves each argument boundary without constructing a command line
        // whose quoting rules would vary with paths and the selected executable.
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.NativeTarProcessStartReturnedFalse,
                    executable));
            }

            // Native extraction is non-interactive. Closing the redirected stream supplies EOF
            // if an unexpected executable attempts to read from standard input.
            process.StandardInput.Close();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return new NativeTarProcessResult(ExitCode: null, StandardError: string.Empty, StartFailure: ex);
        }

        Task<string> standardErrorTask = ReadBoundedAsync(process.StandardError, MaximumStandardErrorLength);
        process.WaitForExit();
        return new NativeTarProcessResult(process.ExitCode, standardErrorTask.GetAwaiter().GetResult());
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, int maximumLength)
    {
        var retained = new StringBuilder(capacity: maximumLength);
        char[] buffer = new char[StandardErrorReadBufferLength];
        int read;
        while ((read = await reader.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            int remaining = maximumLength - retained.Length;
            if (remaining > 0)
            {
                retained.Append(buffer, 0, Math.Min(read, remaining));
            }
        }

        return retained.ToString();
    }
}
