namespace Microsoft.DotNet.Cli.Utils;

/// <summary>
/// Represents the desired verbosity level for command output.
/// Maps mostly to MSBuild's verbosity levels.
/// The odd naming is because we're currently leaning entirely on System.CommandLine's
/// default enum parsing.
/// </summary>
public enum VerbosityOptions
{
    quiet,
    q,
    minimal,
    m,
    normal,
    n,
    detailed,
    d,
    diagnostic,
    diag
}
