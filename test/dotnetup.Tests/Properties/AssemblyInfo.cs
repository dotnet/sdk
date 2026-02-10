// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;

// Enable parallel test execution
[assembly: CollectionBehavior(CollectionBehavior.CollectionPerClass, DisableTestParallelization = false, MaxParallelThreads = 0)]

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Module initializer to configure test environment before any tests run.
/// </summary>
internal static class TestModuleInitializer
{
    /// <summary>
    /// Sets up environment variables for test runs.
    /// This ensures telemetry from tests is marked as dev builds.
    /// </summary>
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Mark all test runs as dev builds so telemetry doesn't pollute production data
        Environment.SetEnvironmentVariable("DOTNETUP_DEV_BUILD", "1");
    }
}
