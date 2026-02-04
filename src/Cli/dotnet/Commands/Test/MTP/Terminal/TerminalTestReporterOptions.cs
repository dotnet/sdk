// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Test.Terminal;

internal sealed class TerminalTestReporterOptions
{
    /// <summary>
    /// Gets path to which all other paths in output should be relative.
    /// </summary>
    public string? BaseDirectory { get; init; }

    /// <summary>
    /// Gets a value indicating whether we should show passed tests.
    /// </summary>
    public bool ShowPassedTests { get; init; }

    /// <summary>
    /// Gets a value indicating whether we should show information about which assembly is the source of the data on screen. Turn this off when running directly from an exe to reduce noise, because the path will always be the same.
    /// </summary>
    public bool ShowAssembly { get; init; }

    /// <summary>
    /// Gets a value indicating whether we should show information about which assembly started or completed. Turn this off when running directly from an exe to reduce noise, because the path will always be the same.
    /// </summary>
    public bool ShowAssemblyStartAndComplete { get; init; }

    /// <summary>
    /// Gets minimum amount of tests to run.
    /// </summary>
    public int MinimumExpectedTests { get; init; }

    /// <summary>
    /// Gets a value indicating whether we should write the progress periodically to screen. When ANSI is allowed we update the progress as often as we can.
    /// When ANSI is not allowed we never have progress.
    /// </summary>
    public bool ShowProgress { get; init; }

    /// <summary>
    /// Gets a value indicating whether the active tests should be visible when the progress is shown.
    /// </summary>
    public bool ShowActiveTests { get; init; }

    /// <summary>
    /// Gets a value indicating the ANSI mode.
    /// </summary>
    public AnsiMode AnsiMode { get; init; }
}

internal enum AnsiMode
{
    /// <summary>
    /// Disable ANSI escape codes.
    /// </summary>
    NoAnsi,

    /// <summary>
    /// Use simplified ANSI renderer, which colors output, but does not move cursor.
    /// This is used in compatible CI environments.
    /// </summary>
    SimpleAnsi,

    /// <summary>
    /// Enable ANSI escape codes, including cursor movement, when the capabilities of the console allow it.
    /// </summary>
    AnsiIfPossible,
}
