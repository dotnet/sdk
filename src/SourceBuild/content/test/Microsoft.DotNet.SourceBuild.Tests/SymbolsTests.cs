// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Microsoft.DotNet.UnifiedBuild.Tasks;

namespace Microsoft.DotNet.SourceBuild.Tests;

public class SymbolsTests : SdkTests
{
    private static string SymbolsTestsRoot { get; } = Path.Combine(Directory.GetCurrentDirectory(), nameof(SymbolsTests));

    public SymbolsTests(ITestOutputHelper outputHelper) : base(outputHelper) { }

    /// <summary>
    /// Verifies that all symbols have valid sourcelinks.
    /// </summary>
    [Fact]
    public void VerifySdkSymbols()
    {
        try
        {
            if (Directory.Exists(SymbolsTestsRoot))
            {
                Directory.Delete(SymbolsTestsRoot, true);
            }
            Directory.CreateDirectory(SymbolsTestsRoot);

            string symbolsRoot = Directory.CreateDirectory(Path.Combine(SymbolsTestsRoot, "symbols")).FullName;

            // We are validating dotnet-symbols-sdk-*.tar.gz which contains source-built sdk symbols
            Utilities.ExtractTarball(
                Utilities.GetFile(Path.GetDirectoryName(Config.SourceBuiltArtifactsPath)!, "dotnet-symbols-sdk-*.tar.gz"),
                symbolsRoot,
                OutputHelper);

            IList<string> failedFiles = VerifySdkFilesHaveMatchingSymbols(symbolsRoot, Config.DotNetDirectory);

            if (failedFiles.Count > 0)
            {
                OutputHelper.WriteLine($"Did not find PDBs for the following SDK files:");
                foreach (string file in failedFiles)
                {
                    OutputHelper.WriteLine(file);
                }
            }

            Assert.True(failedFiles.Count == 0);
        }
        finally
        {
            Directory.Delete(SymbolsTestsRoot, true);
        }
    }

    private IList<string> VerifySdkFilesHaveMatchingSymbols(string symbolsRoot, string sdkRoot)
    {
        Assert.True(Directory.Exists(sdkRoot), $"Path, with SDK files to validate, does not exist: {sdkRoot}");

        // Normalize paths, to ensure proper string replacement
        symbolsRoot = symbolsRoot.TrimEnd(Path.DirectorySeparatorChar);
        sdkRoot = sdkRoot.TrimEnd(Path.DirectorySeparatorChar);

        var failedFiles = new ConcurrentBag<string>();

        IEnumerable<string> allFiles = Directory.GetFiles(sdkRoot, "*", SearchOption.AllDirectories);
        Parallel.ForEach(allFiles, file =>
        {
            if (PdbUtilities.FileInSdkLayoutRequiresAPdb(file, out string guid))
            {
                string symbolFile = Path.ChangeExtension(file.Replace(sdkRoot, symbolsRoot), ".pdb");
                if (!File.Exists(symbolFile))
                {
                    failedFiles.Add(file);
                }
            }
        });

        return failedFiles.ToList();
    }
}
