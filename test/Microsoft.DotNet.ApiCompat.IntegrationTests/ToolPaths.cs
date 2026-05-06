// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiCompat.IntegrationTests
{
    /// <summary>
    /// Helpers for locating the published tool DLLs (apicompat, genapi, apidiff) in the
    /// repo's <c>artifacts/bin/&lt;Tool&gt;/&lt;Configuration&gt;/&lt;TFM&gt;/</c> output directory
    /// so the integration tests can invoke them via <c>dotnet exec</c>.
    /// </summary>
    internal static class ToolPaths
    {
        private static readonly string s_repoRoot =
            SdkTestContext.GetRepoRoot()
            ?? throw new InvalidOperationException("Could not locate the repo root from the test working directory.");

        private static readonly string s_configuration =
#if DEBUG
            "Debug";
#else
            "Release";
#endif

        public static string ApiCompatToolDll { get; } = Resolve("Microsoft.DotNet.ApiCompat.Tool");

        public static string GenAPIToolDll { get; } = Resolve("Microsoft.DotNet.GenAPI.Tool");

        public static string ApiDiffToolDll { get; } = Resolve("Microsoft.DotNet.ApiDiff.Tool");

        private static string Resolve(string toolName)
        {
            string toolBinDir = Path.Combine(s_repoRoot, "artifacts", "bin", toolName, s_configuration);
            if (!Directory.Exists(toolBinDir))
            {
                throw new DirectoryNotFoundException(
                    $"Could not find the build output directory for {toolName} at '{toolBinDir}'. " +
                    $"Make sure {toolName}.csproj has been built (it should be a build-ordering dependency of the IntegrationTests project).");
            }

            // The tool projects single-target $(NetMinimum); discover the actual TFM directory rather
            // than hard-coding a version that drifts over time.
            string? path = Directory.EnumerateFiles(toolBinDir, $"{toolName}.dll", SearchOption.AllDirectories)
                .OrderByDescending(p => p)
                .FirstOrDefault(p => !p.Contains("publish", StringComparison.OrdinalIgnoreCase));

            if (path is null)
            {
                throw new FileNotFoundException(
                    $"Could not find the {toolName} entry-point DLL under '{toolBinDir}'. " +
                    $"Make sure {toolName}.csproj has been built (it should be a build-ordering dependency of the IntegrationTests project).");
            }
            return path;
        }
    }
}
