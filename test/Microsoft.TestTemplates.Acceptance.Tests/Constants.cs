// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.TestTemplates.Acceptance.Tests;

internal static class Constants
{
#if DEBUG
    public const string BuildConfiguration = "Debug";
#else
    public const string BuildConfiguration = "Release";
#endif

    public static readonly string ArtifactsTempDirectory = Path.Combine(FindRepoRoot(), "artifacts", "tmp", BuildConfiguration);

    private static string FindRepoRoot()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        while (currentDirectory != null)
        {
            if (Directory.Exists(Path.Combine(currentDirectory, ".git")))
            {
                return currentDirectory;
            }

            currentDirectory = Directory.GetParent(currentDirectory)?.FullName;
        }

        throw new DirectoryNotFoundException("Could not find the root of the repo.");
    }
}
