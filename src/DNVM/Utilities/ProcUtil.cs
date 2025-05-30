
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Dnvm;

public static class ProcUtil
{
    public static async Task<ProcResult> RunWithOutput(string cmd, string args, Dictionary<string, string>? envVars = null)
    {
        var psi = new ProcessStartInfo {
            FileName = cmd,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        if (envVars is not null)
        {
            foreach (var (k, v) in envVars)
            {
                psi.Environment[k] = v;
            }
        }
        var proc = Process.Start(psi);
        if (proc is null)
        {
            throw new IOException($"Could not start process: " + cmd);
        }

        var @out = proc.StandardOutput.ReadToEndAsync();
        var error = proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync().ConfigureAwait(false);
        return new ProcResult(
            proc.ExitCode,
            await @out.ConfigureAwait(false),
            await error.ConfigureAwait(false));
    }

    public static async Task<ProcResult> RunShell(
        string scriptContent,
        Dictionary<string, string>? envVars = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true
        };
        if (envVars is not null)
        {
            foreach (var (k, v) in envVars)
            {
                psi.Environment[k] = v;
            }
        }
        var proc = Process.Start(psi)!;
        await proc.StandardInput.WriteAsync(scriptContent);
        proc.StandardInput.Close();
        var @out = proc.StandardOutput.ReadToEndAsync();
        var error = proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync().ConfigureAwait(false);
        return new ProcResult(
            proc.ExitCode,
            await @out.ConfigureAwait(false),
            await error.ConfigureAwait(false));
    }

    public sealed record ProcResult(int ExitCode, string Out, string Error);
}