// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Test.Terminal;

/// <summary>
/// Outcome of a test.
/// </summary>
internal enum TestOutcome
{
    /// <summary>
    /// Error.
    /// </summary>
    Error,

    /// <summary>
    /// Fail.
    /// </summary>
    Fail,

    /// <summary>
    /// Passed.
    /// </summary>
    Passed,

    /// <summary>
    /// Skipped.
    /// </summary>
    Skipped,

    /// <summary>
    ///  Timeout.
    /// </summary>
    Timeout,

    /// <summary>
    /// Canceled.
    /// </summary>
    Canceled,
}
