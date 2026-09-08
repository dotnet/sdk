// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
