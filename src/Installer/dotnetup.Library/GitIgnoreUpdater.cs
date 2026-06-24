// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Maintains the project .gitignore entry needed for repo-local .NET SDK installs.
/// Assumes the .gitignore file lives next to the local .dotnet directory configured in global.json.
/// </summary>
internal static class GitIgnoreUpdater
{
    private const string DotnetIgnoreEntry = ".dotnet/";

    public static void EnsureDotnetDirectoryIgnored(string projectDirectory)
    {
        string gitIgnorePath = Path.Combine(projectDirectory, ".gitignore");
        if (!File.Exists(gitIgnorePath))
        {
            File.WriteAllText(gitIgnorePath, DotnetIgnoreEntry + Environment.NewLine);
            return;
        }

        string content = File.ReadAllText(gitIgnorePath);
        if (ContainsDotnetIgnoreEntry(content))
        {
            return;
        }

        string newline = content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        string prefix = content.Length == 0 || content.EndsWith('\n') || content.EndsWith('\r')
            ? string.Empty
            : newline;

        File.AppendAllText(gitIgnorePath, prefix + DotnetIgnoreEntry + newline);
    }

    private static bool ContainsDotnetIgnoreEntry(string content)
    {
        var lines = content.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
        return lines.Any(line =>
        {
            string trimmed = line.Trim();
            return string.Equals(trimmed, DotnetIgnoreEntry, StringComparison.Ordinal)
                || string.Equals(trimmed, ".dotnet", StringComparison.Ordinal)
                || string.Equals(trimmed, "/" + DotnetIgnoreEntry, StringComparison.Ordinal);
        });
    }
}
