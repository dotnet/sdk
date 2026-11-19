// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.Versioning;

namespace Microsoft.DotNet.Tools.Bootstrapper;

internal static class WindowsElevation
{
    /// <summary>
    /// Starts an elevated process to modify the system PATH and waits for it to complete.
    /// </summary>
    /// <param name="operation">The elevated operation to perform (<c>insertdotnet</c>/<c>removedotnet</c>).</param>
    /// <param name="dotnetDir">The dotnet directory the elevated process should insert into or remove
    /// from the system PATH. This must be supplied by the caller because the elevated process can run
    /// under a different account than the invoking user — when a standard user elevates by supplying
    /// an administrator's credentials, the child runs as that administrator, whose per-user directory
    /// (e.g. %LOCALAPPDATA%) differs — so the invoking user's directory cannot be recomputed there.</param>
    /// <exception cref="DotnetInstallException">Thrown when the user declines the UAC elevation prompt.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the process cannot be started or returns a non-zero exit code.</exception>
    [SupportedOSPlatform("windows")]
    public static void StartElevatedProcess(string operation, string dotnetDir)
    {
        var processPath = DotnetupProcessInfo.ExecutablePath;
        if (string.IsNullOrEmpty(processPath))
        {
            throw new InvalidOperationException("Unable to determine current process path.");
        }

        //  We can't capture output directly from an elevated process, so we pass it a filename where
        //  it should write any output that should be displayed
        var tempDirectory = Directory.CreateTempSubdirectory("dotnetup_elevated");
        string outputFilePath = Path.Combine(tempDirectory.FullName, "output.txt");

        try
        {
            string arguments = $"elevatedsystempath {operation} \"{outputFilePath}\" --dotnet-dir \"{dotnetDir}\"";

            var startInfo = new ProcessStartInfo
            {
                FileName = processPath,
                Arguments = arguments,
                Verb = "runas", // This triggers UAC elevation
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            RunElevatedProcessCore(startInfo);
        }
        finally
        {
            DisplayElevatedProcessOutput(outputFilePath);

            // Clean up temporary directory
            try
            {
                tempDirectory.Delete(recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    /// <summary>
    /// Starts the elevated process described by <paramref name="startInfo"/>, waits for it to exit,
    /// and validates the exit code.
    /// </summary>
    /// <exception cref="DotnetInstallException">Thrown when the user cancels the UAC prompt.</exception>
    private static void RunElevatedProcessCore(ProcessStartInfo startInfo)
    {
        try
        {
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start elevated process.");
            }

            process.WaitForExit();

            if (process.ExitCode == -2147450730)
            {
                //  NOTE: Process exit code -2147450730 means that the right .NET runtime could not be found
                //  This should not happen when using NativeAOT dotnetup, but when testing using IL it can happen and
                //  can be caused if DOTNET_ROOT has been set to a path that doesn't have the right runtime to run dotnetup.
                throw new InvalidOperationException("Elevated process failed: Unable to find matching .NET Runtime." + Environment.NewLine +
                    "This is probably because dotnetup is not being run as self-contained and DOTNET_ROOT is set to a path that doesn't have a matching runtime.");
            }
            else if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Elevated process returned exit code {process.ExitCode}");
            }
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            // User cancelled UAC prompt or elevation failed
            // ERROR_CANCELLED = 1223
            if (ex.NativeErrorCode == 1223)
            {
                // Surface the declined prompt as a clean, user-category error (no stack trace)
                // instead of returning a status that every caller would have to re-wrap.
                throw new DotnetInstallException(
                    DotnetInstallErrorCode.PermissionDenied,
                    Strings.EnvElevationCancelled);
            }
            throw;
        }
    }

    /// <summary>
    /// Reads and displays any output written by the elevated process to <paramref name="outputFilePath"/>.
    /// </summary>
    internal static void DisplayElevatedProcessOutput(string outputFilePath)
    {
        if (File.Exists(outputFilePath))
        {
            string outputContent = File.ReadAllText(outputFilePath);
            if (!string.IsNullOrEmpty(outputContent))
            {
                Console.WriteLine(outputContent);
            }
        }
    }
}
