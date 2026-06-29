// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Stdin-forwarding test isolated in its own xUnit collection
/// (<see cref="DotnetupProcessLaunchTests"/>) so it never runs concurrently with other
/// CPU/IO-heavy tests in the assembly. The test launches the real dotnetup executable
/// as a child process, redirects stdin/stdout/stderr, and waits for the grandchild
/// (fake <c>dotnet</c>) to drain a pipe — concurrent test load on the CI agent has
/// caused intermittent timeouts on this pipeline before. Serialization removes the
/// contention; runtime cost is negligible (one test).
/// </summary>
[Collection("DotnetupProcessLaunchTests")]
public class DotnetCommandStdinForwardingTests
{
    /// <summary>
    /// Verifies that interactive stdin data is properly forwarded from the
    /// parent process through dotnetup to the child dotnet process.
    /// This is critical for commands like <c>dotnet nuget push --interactive</c>
    /// or <c>dotnet new</c> that prompt for user input.
    ///
    /// The test launches dotnetup as a real process, redirects its stdin,
    /// writes test data, and verifies the child process echoes it back
    /// through stdout — proving the full stdin forwarding pipeline works.
    /// </summary>
    [Fact]
    public void DotnetCommand_ForwardsStdinToChildProcess()
    {
        var tempDir = Directory.CreateTempSubdirectory("dotnetup-stdin-test");
        try
        {
            DotnetCommandTests.CreateStdinEchoFakeDotnet(tempDir.FullName);

            string dotnetupPath = DotnetupTestUtilities.GetDotnetupExecutablePath();
            string testInput = "HelloInteractive_" + Guid.NewGuid().ToString("N")[..8];

            using var process = new Process();
            process.StartInfo.FileName = dotnetupPath;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            // Prepend our temp dir to PATH so dotnetup resolves our fake dotnet
            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            process.StartInfo.Environment["PATH"] = tempDir.FullName + Path.PathSeparator + currentPath;
            process.StartInfo.Environment["DOTNET_NOLOGO"] = "1";
            process.StartInfo.Environment["NO_COLOR"] = "1";

            if (OperatingSystem.IsWindows())
            {
                // Fake dotnet.exe is cmd.exe; forward /c "findstr /r ." to echo stdin.
                // findstr reads from stdin and echoes lines matching regex "." (non-empty).
                process.StartInfo.Arguments = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(
                    ["dotnet", "/c", "findstr /r ."]);
            }
            else
            {
                // Fake dotnet is a shell script that cats stdin to stdout.
                process.StartInfo.Arguments = "dotnet";
            }

            process.Start();

            // Write test data to dotnetup's stdin; the child process inherits this
            // stdin handle because DotnetCommand uses RedirectStandardInput = false.
            process.StandardInput.WriteLine(testInput);
            process.StandardInput.Close();

            string output = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            // The child process should have received and echoed our input
            output.Should().Contain(testInput,
                because: "stdin data should be forwarded to the child dotnet process for interactive commands");
        }
        finally
        {
            try { tempDir.Delete(recursive: true); } catch { /* cleanup best-effort */ }
        }
    }
}
