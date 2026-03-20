// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using VerifyTests.DiffPlex;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public static class ModuleInitializer
    {
        [ModuleInitializer]
        public static void Init()
        {
            DerivePathInfo(
                (sourceFile, _, type, method) =>
                {
                    // When the source directory is unavailable (e.g., Helix CI), the Verify library cannot
                    // resolve the snapshot directory from the [CallerFilePath] embedded in the PDB.
                    // Fall back to an absolute path next to the test DLL, which the project copies the
                    // Approvals directory to via CopyToOutputDirectory.
                    var sourceDir = Path.GetDirectoryName(sourceFile);
                    string directory = sourceDir is not null && Directory.Exists(sourceDir)
                        ? "Approvals"
                        : Path.Combine(AppContext.BaseDirectory, "ParserTests", "Approvals");
                    return new PathInfo(
                        directory: directory,
                        typeName: type.Name,
                        methodName: method.Name);
                });

            // Customize diff output of verifier
            VerifyDiffPlex.Initialize(OutputType.Compact);
        }
    }
}
