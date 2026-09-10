// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Run.Tests;

/// <summary>
/// Performs the one-time warmup required by file-based application tests.
/// </summary>
public sealed class RunFileTestFixture
{
    private static bool s_initialized;
    private static readonly object s_lock = new();

    /// <summary>
    /// Warms file-based execution and initializes the isolated run-file environment once.
    /// </summary>
    /// <param name="log">The test output logger.</param>
    public static void EnsureInitialized(ITestOutputHelper log)
    {
        if (s_initialized)
        {
            return;
        }

        lock (s_lock)
        {
            if (s_initialized)
            {
                return;
            }

            RunFileTestBase.CopyNuGetConfigToRunfileDirectory();

            // `dotnet run -` falls back to project-based run when the working directory contains a project.
            // Use a stable project-free directory because other tests can temporarily change the
            // process-wide current directory.
            new DotnetCommand(log, "run", "-")
                .WithWorkingDirectory(RunFileTestBase.OutOfTreeBaseDirectory)
                .WithStandardInput("""
                    Console.WriteLine("Hello");
                    """)
                .Execute()
                .Should().Pass()
                .And.HaveStdOut("Hello");

            s_initialized = true;
        }
    }
}
