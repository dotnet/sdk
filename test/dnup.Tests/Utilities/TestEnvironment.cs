// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.DotNet.Tools.Dnup.Tests.Utilities;

/// <summary>
/// Represents a temporary test environment with isolated directories and environment variables
/// </summary>
internal class TestEnvironment : IDisposable
{
    private readonly string? _originalManifestPath;
    private readonly string? _originalDefaultInstallPath;
    private readonly string _originalCurrentDirectory;

    public string TempRoot { get; }
    public string InstallPath { get; }
    public string ManifestPath { get; }

    public TestEnvironment(string tempRoot, string installPath, string manifestPath)
    {
        TempRoot = tempRoot;
        InstallPath = installPath;
        ManifestPath = manifestPath;

        // Store original environment values to restore later
        _originalManifestPath = Environment.GetEnvironmentVariable("DOTNET_TESTHOOK_MANIFEST_PATH");
        _originalDefaultInstallPath = Environment.GetEnvironmentVariable("DOTNET_TESTHOOK_DEFAULT_INSTALL_PATH");
        _originalCurrentDirectory = Environment.CurrentDirectory;

        // Set test environment variables
        Environment.SetEnvironmentVariable("DOTNET_TESTHOOK_MANIFEST_PATH", manifestPath);
        Environment.SetEnvironmentVariable("DOTNET_TESTHOOK_DEFAULT_INSTALL_PATH", installPath);

        // Change current directory to the temp directory to avoid global.json in repository root
        Environment.CurrentDirectory = tempRoot;
    }

    public void Dispose()
    {
        // Restore original environment
        Environment.CurrentDirectory = _originalCurrentDirectory;
        Environment.SetEnvironmentVariable("DOTNET_TESTHOOK_MANIFEST_PATH", _originalManifestPath);
        Environment.SetEnvironmentVariable("DOTNET_TESTHOOK_DEFAULT_INSTALL_PATH", _originalDefaultInstallPath);

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
}
