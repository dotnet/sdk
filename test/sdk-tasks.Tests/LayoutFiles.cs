// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.TestFramework;

namespace Microsoft.CoreSdkTasks.Tests;

/// <summary>
/// Finds production layout files that tests build against.
/// </summary>
internal static class LayoutFiles
{
    /// <summary>
    /// Returns the copy deployed under <c>Layout/</c> when tests run from a test execution directory
    /// (for example, on Helix). Otherwise returns the repository file, so local runs use current sources.
    /// </summary>
    public static string GetPath(string deployedRelativePath, string repositoryRelativePath)
    {
        string deployedPath = Path.Combine(SdkTestContext.Current.TestExecutionDirectory, "Layout", deployedRelativePath);

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_SDK_TEST_EXECUTION_DIRECTORY"))
            && File.Exists(deployedPath))
        {
            return deployedPath;
        }

        string? repoRoot = SdkTestContext.Current.ToolsetUnderTest.RepoRoot ?? SdkTestContext.GetRepoRoot();

        if (repoRoot is not null)
        {
            string repoPath = Path.Combine(repoRoot, repositoryRelativePath);

            if (File.Exists(repoPath))
            {
                return repoPath;
            }
        }

        if (File.Exists(deployedPath))
        {
            return deployedPath;
        }

        throw new InvalidOperationException($"Could not find {deployedRelativePath}.");
    }
}
