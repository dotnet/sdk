// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

/// <summary>
/// Represents a temporary test environment with isolated directories and environment variables
/// </summary>
internal class TestEnvironment : IDisposable
{
    private readonly bool _configureEnvironment;
    private readonly string _originalCurrentDirectory;

    public string TempRoot { get; }
    public string InstallPath { get; }
    public string ManifestPath { get; }

    /// <summary>
    /// Creates a test environment with isolated temporary directories.
    /// </summary>
    /// <param name="configureEnvironment">
    /// When true, sets <c>DOTNET_TESTHOOK_DEFAULT_INSTALL_PATH</c> and <c>DOTNET_TESTHOOK_MANIFEST_PATH</c>,
    /// and changes the current directory to the temp root to avoid picking up the repository's global.json.
    /// These are restored on Dispose.
    /// </param>
    public TestEnvironment(bool configureEnvironment = false)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "dotnetup-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        // Resolve symlinks in the temp path (e.g. on macOS /var -> /private/var).
        // Child processes resolve these via getcwd(), so test paths must match.
        TempRoot = ResolveRealPath(tempRoot);
        InstallPath = Path.Combine(TempRoot, "dotnet");
        var dotnetupDir = Path.Combine(TempRoot, "dotnetup");
        Directory.CreateDirectory(InstallPath);
        Directory.CreateDirectory(dotnetupDir);
        ManifestPath = Path.Combine(dotnetupDir, "manifest.json");

        _configureEnvironment = configureEnvironment;
        _originalCurrentDirectory = TempRoot;

        if (configureEnvironment)
        {
            try
            {
                _originalCurrentDirectory = Environment.CurrentDirectory;
            }
            catch (Exception ex)
            {
                // If we can't get the current directory (which can happen in CI),
                // use the temp directory as a fallback
                Console.WriteLine($"Warning: Could not get current directory: {ex.Message}. Using temp directory as fallback.");
            }

            // Set default install path and manifest path as environment variables
            // so tests are fully isolated from each other and from the real installation.
            Environment.SetEnvironmentVariable("DOTNET_TESTHOOK_DEFAULT_INSTALL_PATH", InstallPath);
            Environment.SetEnvironmentVariable("DOTNET_TESTHOOK_MANIFEST_PATH", ManifestPath);

            // Change current directory to the temp directory to avoid global.json in repository root
            Environment.CurrentDirectory = TempRoot;
        }
    }

    public void Dispose()
    {
        if (_configureEnvironment)
        {
            try
            {
                // Restore original environment
                Environment.CurrentDirectory = _originalCurrentDirectory;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not restore current directory: {ex.Message}");
            }

            // Clear the environment variables we set
            Environment.SetEnvironmentVariable("DOTNET_TESTHOOK_DEFAULT_INSTALL_PATH", null);
            Environment.SetEnvironmentVariable("DOTNET_TESTHOOK_MANIFEST_PATH", null);
        }

        // Clean up
        if (Directory.Exists(TempRoot))
        {
            try
            {
                Directory.Delete(TempRoot, recursive: true);
            }
            catch (IOException)
            {
                // Files might be locked, but we tried our best to clean up
                Console.WriteLine($"Warning: Could not clean up temp directory: {TempRoot}");
            }
        }
    }

    /// <summary>
    /// Resolves all symlinks in a directory path by walking each component.
    /// For example, on macOS this resolves /var/folders/... to /private/var/folders/...
    /// </summary>
    private static string ResolveRealPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath)!;
        var resolved = root;

        foreach (var component in fullPath[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            resolved = Path.Combine(resolved, component);
            var target = Directory.ResolveLinkTarget(resolved, returnFinalTarget: true);
            if (target != null)
            {
                resolved = target.FullName;
            }
        }

        return resolved;
    }
}
