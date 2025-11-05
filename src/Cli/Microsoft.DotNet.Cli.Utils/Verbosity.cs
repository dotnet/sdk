namespace Microsoft.DotNet.Cli.Utils;

/// <summary>
/// Represents the desired verbosity level for command output.
/// Maps mostly to MSBuild's verbosity levels.
/// </summary>
public enum Verbosity
{
    quiet,
    minimal,
    normal,
    detailed,
    diagnostic
}

public static class VerbosityData
{
    public static string[] VerbosityNames =
    [
        "quiet",
        "q",
        "minimal",
        "m",
        "normal",
        "n",
        "detailed",
        "d",
        "diagnostic",
        "diag"
    ];
}
