// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff.IntegrationTests
{
    /// <summary>
    /// Locates the published <c>apidiff</c> tool DLL that the IntegrationTests project's
    /// <c>_StageToolsUnderTest</c> target copies under <c>tools\Microsoft.DotNet.ApiDiff.Tool\</c>
    /// in the test assembly's output directory. Resolving relative to
    /// <see cref="AppContext.BaseDirectory"/> works both for local <c>dotnet test</c> runs and on
    /// Helix work items (where the tools tree is part of the work item payload).
    /// </summary>
    internal static class ToolPaths
    {
        public static string ApiDiffToolDll { get; } = Resolve("Microsoft.DotNet.ApiDiff.Tool");

        private static string Resolve(string toolName)
        {
            string toolDir = Path.Combine(AppContext.BaseDirectory, "tools", toolName);
            string path = Path.Combine(toolDir, $"{toolName}.dll");
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"Could not find the {toolName} entry-point DLL at '{path}'. " +
                    $"Make sure the IntegrationTests project's _StageToolsUnderTest target ran (it copies the tool's bin output under '{toolDir}').",
                    path);
            }
            return path;
        }
    }
}
