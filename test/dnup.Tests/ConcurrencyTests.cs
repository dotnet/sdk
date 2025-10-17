// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dnup.Tests.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tools.Dnup.Tests;

/// <summary>
/// Tests that verify concurrency behavior of dnup installations.
/// Tests that multiple installations can happen concurrently without conflicts.
/// </summary>
[Collection("DnupConcurrencyCollection")]
public class ConcurrencyEndToEndTests
{
    /// <summary>
    /// Test that verifies that multiple dnup instances can run simultaneously
    /// without conflicts by using different install paths
    /// </summary>
    [Fact]
    public async Task TestMultipleDnupInstances()
    {
        // We'll install multiple versions concurrently
        var installTasks = new List<Task<bool>>();

        // Install different versions concurrently
        installTasks.Add(InstallSdkAsync("9.0.100"));
        installTasks.Add(InstallSdkAsync("9.0.101"));
        installTasks.Add(InstallSdkAsync("9.0.102"));

        // Wait for all installations to complete
        var results = await Task.WhenAll(installTasks);

        // Verify all installations succeeded
        foreach (var result in results)
        {
            result.Should().BeTrue("All installations should succeed");
        }
    }

    /// <summary>
    /// Installs an SDK asynchronously in its own isolated environment
    /// </summary>
    private async Task<bool> InstallSdkAsync(string version)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var testEnv = DnupTestUtilities.CreateTestEnvironment();
                var args = DnupTestUtilities.BuildArguments(version, testEnv.InstallPath);

                Console.WriteLine($"Installing SDK {version}");
                int exitCode = Parser.Parse(args).Invoke();

                if (exitCode != 0)
                {
                    Console.WriteLine($"Installation of {version} failed with exit code {exitCode}");
                    return false;
                }

                // Verify the installation was recorded in the manifest
                using var finalizeLock = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
                var manifest = new DnupSharedManifest();
                var installs = manifest.GetInstalledVersions();

                var matchingInstalls = installs.Where(i => PathUtilities.PathsEqual(i.InstallRoot.Path, testEnv.InstallPath) &&
                                                        i.Version.ToString() == version).ToList();

                if (matchingInstalls.Count != 1)
                {
                    Console.WriteLine($"Expected 1 installation of {version}, but found {matchingInstalls.Count}");
                    return false;
                }

                Console.WriteLine($"Installation of {version} completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error installing {version}: {ex.Message}");
                return false;
            }
        });
    }
}
