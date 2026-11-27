// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.CommandLine.StaticCompletions;

public enum ShellType
{
    Unknown,
    Bash,
    Sh,
    Dash,
    Zsh,
    Fish,
    PowerShell,
    Nushell,
}

public static class ShellInfo
{
    public static ShellType GetShellType(string? shellPath)
    {
        var shellName = Path.GetFileName(shellPath);
        return shellName?.ToLowerInvariant() switch
        {
            "bash" => ShellType.Bash,
            "sh" => ShellType.Sh,
            "dash" => ShellType.Dash,
            "zsh" => ShellType.Zsh,
            "fish" => ShellType.Fish,
            "pwsh" or "powershell" => ShellType.PowerShell,
            "nu" or "nushell" => ShellType.Nushell,
            _ => ShellType.Unknown,
        };
    }

    public static string? GetCanonicalName(ShellType shell) => shell switch
    {
        ShellType.Bash => "bash",
        ShellType.Sh => "sh",
        ShellType.Dash => "dash",
        ShellType.Zsh => "zsh",
        ShellType.Fish => "fish",
        ShellType.PowerShell => "pwsh",
        ShellType.Nushell => "nushell",
        _ => null,
    };

    public static string QuoteLiteralArgument(ShellType shell, string value) => shell switch
    {
        ShellType.Bash or ShellType.Sh or ShellType.Dash or ShellType.Zsh =>
            "'" + value.Replace("'", "'\\''") + "'",
        ShellType.Fish =>
            "'" + value.Replace("\\", "\\\\").Replace("'", "\\'") + "'",
        ShellType.PowerShell =>
            "'" + value
                .Replace("'", "''")
                .Replace("\u2018", "\u2018\u2018")
                .Replace("\u2019", "\u2019\u2019")
                .Replace("\u201a", "\u201a\u201a")
                .Replace("\u201b", "\u201b\u201b") + "'",
        ShellType.Nushell => QuoteNushellLiteralArgument(value),
        _ => throw new ArgumentOutOfRangeException(nameof(shell), shell, "The shell must be known to quote an argument."),
    };

    private static string QuoteNushellLiteralArgument(string value)
    {
        var delimiter = "#";
        while (value.Contains("'" + delimiter))
        {
            delimiter += "#";
        }

        return "r" + delimiter + "'" + value + "'" + delimiter;
    }
}
