// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Dotnet.Installation;

namespace Microsoft.DotNet.Tools.Dnup.Tests.Utilities;

/// <summary>
/// Common utilities for dnup tests
/// </summary>
internal static class DnupTestUtilities
{
    /// <summary>
    /// Creates a test environment with proper temporary directories
    /// </summary>
    public static TestEnvironment CreateTestEnvironment()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "dnup-e2e", Guid.NewGuid().ToString("N"));
        string installPath = Path.Combine(tempRoot, "dotnet-root");
        string manifestPath = Path.Combine(tempRoot, "dnup_manifest.json");

        // Create necessary directories
        Directory.CreateDirectory(tempRoot);
        Directory.CreateDirectory(installPath);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);

        return new TestEnvironment(tempRoot, installPath, manifestPath);
    }

    /// <summary>
    /// Builds command line arguments for dnup
    /// </summary>
    public static string[] BuildArguments(string channel, string installPath, string? manifestPath = null, bool disableProgress = true)
    {
        var args = new List<string>
        {
            "sdk",
            "install",
            channel
        };

        args.Add("--install-path");
        args.Add(installPath);
        args.Add("--interactive");
        args.Add("false");

        // Add manifest path option if specified for test isolation
        if (!string.IsNullOrEmpty(manifestPath))
        {
            args.Add("--manifest-path");
            args.Add(manifestPath);
        }

        // Add no-progress option when running tests in parallel to avoid Spectre.Console exclusivity issues
        if (disableProgress)
        {
            args.Add("--no-progress");
        }

        return [.. args];
    }

    /// <summary>
    /// Maps System.Runtime.InteropServices.Architecture to Microsoft.Dotnet.Installation.InstallArchitecture
    /// </summary>
    public static InstallArchitecture MapArchitecture(Architecture architecture) =>
        Microsoft.DotNet.Tools.Bootstrapper.DnupUtilities.GetInstallArchitecture(architecture);
}
