// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

/// <summary>
/// Represents a temporary test environment with isolated directories.
/// All isolation is achieved via explicit paths passed to child processes —
/// no process-wide state (CWD, env vars) is mutated, so tests in different
/// xUnit collections can safely run in parallel.
/// </summary>
internal class TestEnvironment : IDisposable
{
    public string TempRoot { get; }
    public string InstallPath { get; }
    public string ManifestPath { get; }

    public TestEnvironment(string tempRoot, string installPath, string manifestPath)
    {
        TempRoot = tempRoot;
        InstallPath = installPath;
        ManifestPath = manifestPath;
    }

    public void Dispose()
    {
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
