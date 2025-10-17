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
        string manifestPath = Path.Combine(tempRoot, "manifest", "dnup_manifest.json");

        // Create necessary directories
        Directory.CreateDirectory(tempRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        Directory.CreateDirectory(installPath);

        return new TestEnvironment(tempRoot, installPath, manifestPath);
    }

    /// <summary>
    /// Builds command line arguments for dnup
    /// </summary>
    public static string[] BuildArguments(string channel, string installPath, bool disableProgress = true)
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

        // Add no-progress option when running tests in parallel to avoid Spectre.Console exclusivity issues
        if (disableProgress)
        {
            args.Add("--no-progress");
        }

        return args.ToArray();
    }

    /// <summary>
    /// Maps System.Runtime.InteropServices.Architecture to Microsoft.Dotnet.Installation.InstallArchitecture
    /// </summary>
    public static InstallArchitecture MapArchitecture(Architecture architecture) => architecture switch
    {
        Architecture.X86 => InstallArchitecture.x86,
        Architecture.X64 => InstallArchitecture.x64,
        Architecture.Arm64 => InstallArchitecture.arm64,
        _ => throw new NotSupportedException($"Architecture {architecture} is not supported."),
    };
}
