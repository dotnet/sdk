// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.Cli.Telemetry;

internal class LLMProcessTreeDetector : ILLMProcessTreeDetector
{
    // Maps process names (lowercase) to LLM identifiers reported in telemetry.
    private static readonly Dictionary<string, string> s_processNameToLLMId = new(StringComparer.OrdinalIgnoreCase)
    {
        ["claude"] = "claude",
        ["copilot"] = "copilot",
        ["cursor"] = "cursor",
        ["code"] = "vscode",
        ["windsurf"] = "windsurf",
        ["zed"] = "zed",
        ["gemini"] = "gemini",
        ["codex"] = "codex",
        ["aider"] = "aider",
        ["goose"] = "goose",
        ["amp"] = "amp",
    };

    private const int MaxAncestorDepth = 20;

    /// <inheritdoc/>
    public string? GetLLMFromProcessTree()
    {
        try
        {
            var matches = new List<string>();
            var visited = new HashSet<int>();
            int currentPid = Environment.ProcessId;
            visited.Add(currentPid);

            for (int depth = 0; depth < MaxAncestorDepth; depth++)
            {
                int parentPid = GetParentProcessId(currentPid);
                if (parentPid <= 0 || !visited.Add(parentPid))
                {
                    break;
                }

                string? processName = GetProcessName(parentPid);
                if (processName != null)
                {
                    string? llmId = GetLLMIdFromProcessName(processName);
                    if (llmId != null && !matches.Contains(llmId))
                    {
                        matches.Add(llmId);
                    }
                }

                currentPid = parentPid;
            }

            return matches.Count > 0 ? string.Join(", ", matches) : null;
        }
        catch
        {
            // Telemetry must never crash the CLI.
            return null;
        }
    }

    private static string? GetLLMIdFromProcessName(string processName)
    {
        // Exact match first (covers most cases).
        if (s_processNameToLLMId.TryGetValue(processName, out string? llmId))
        {
            return llmId;
        }

        // Prefix match handles update-renamed binaries like "copilot.exe.old".
        foreach (var kvp in s_processNameToLLMId)
        {
            if (processName.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase) &&
                processName.Length > kvp.Key.Length &&
                processName[kvp.Key.Length] == '.')
            {
                return kvp.Value;
            }
        }

        return null;
    }

    private static string? GetProcessName(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.ProcessName;
        }
        catch
        {
            return null;
        }
    }

    private static int GetParentProcessId(int pid)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return GetParentProcessIdWindows(pid);
            }
            else if (OperatingSystem.IsLinux())
            {
                return GetParentProcessIdLinux(pid);
            }
            else if (OperatingSystem.IsMacOS())
            {
                return GetParentProcessIdMacOS(pid);
            }

            return -1;
        }
        catch
        {
            return -1;
        }
    }

    private static int GetParentProcessIdWindows(int pid)
    {
#if NET
        if (!OperatingSystem.IsWindows())
        {
            return -1;
        }
#endif
        try
        {
            using var process = Process.GetProcessById(pid);
            return Microsoft.DotNet.Cli.Utils.Extensions.ProcessExtensions.GetParentProcessId(process);
        }
        catch
        {
            return -1;
        }
    }

    private static int GetParentProcessIdLinux(int pid)
    {
        // Read /proc/{pid}/stat — field index 3 (0-based) is the ppid.
        try
        {
            string statPath = $"/proc/{pid}/stat";
            if (!File.Exists(statPath))
            {
                return -1;
            }

            string stat = File.ReadAllText(statPath);
            // The process name in field 2 may contain spaces/parens, so find the last ')' to skip past it.
            int lastParen = stat.LastIndexOf(')');
            if (lastParen < 0 || lastParen + 2 >= stat.Length)
            {
                return -1;
            }

            string[] fields = stat[(lastParen + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            // fields[0] = state, fields[1] = ppid
            if (fields.Length >= 2 && int.TryParse(fields[1], out int ppid))
            {
                return ppid;
            }

            return -1;
        }
        catch
        {
            return -1;
        }
    }

    private static int GetParentProcessIdMacOS(int pid)
    {
        try
        {
            using var ps = new Process();
            ps.StartInfo.FileName = "ps";
            ps.StartInfo.Arguments = $"-o ppid= -p {pid}";
            ps.StartInfo.RedirectStandardOutput = true;
            ps.StartInfo.UseShellExecute = false;
            ps.StartInfo.CreateNoWindow = true;
            ps.Start();

            string output = ps.StandardOutput.ReadToEnd().Trim();
            ps.WaitForExit(1000);

            if (int.TryParse(output, out int ppid))
            {
                return ppid;
            }

            return -1;
        }
        catch
        {
            return -1;
        }
    }
}
