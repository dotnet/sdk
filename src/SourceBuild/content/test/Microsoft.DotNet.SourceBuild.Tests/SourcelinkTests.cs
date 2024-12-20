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

namespace Microsoft.DotNet.SourceBuild.Tests;

/// <summary>
/// Separate test collection for Sourcelink tests. This is needed due to intra-test parallelization,
/// which can cause less CPU time to be allocated to other tests.
/// This would make other tests run too long and fail due to timeouts.
/// </summary>
[CollectionDefinition(nameof(SourcelinkTestCollection), DisableParallelization = true)]
public class SourcelinkTestCollection { }

[Collection(nameof(SourcelinkTestCollection))]
public class SourcelinkTests : SdkTests
{
    private static string SourcelinkRoot { get; } = Path.Combine(Directory.GetCurrentDirectory(), nameof(SourcelinkTests));

    public SourcelinkTests(ITestOutputHelper outputHelper) : base(outputHelper) { }

    /// <summary>
    /// Verifies that all symbols have valid sourcelinks.
    /// </summary>
    [Fact]
    public void VerifySourcelinks()
    {
        try
        {
            if (Directory.Exists(SourcelinkRoot))
            {
                Directory.Delete(SourcelinkRoot, true);
            }
            Directory.CreateDirectory(SourcelinkRoot);

            string symbolsRoot = Directory.CreateDirectory(Path.Combine(SourcelinkRoot, "symbols")).FullName;

            // We are validating dotnet-symbols-all-*.tar.gz which contains all source-built symbols, including
            // SDK-specific symbols that are also packaged in dotnet-symbols-sdk-*.tar.gz.
            Utilities.ExtractTarball(
                Utilities.GetFile(Path.GetDirectoryName(Config.SourceBuiltArtifactsPath)!, "dotnet-symbols-all-*.tar.gz"),
                symbolsRoot,
                OutputHelper);

            IList<string> failedFiles = ValidateSymbols(symbolsRoot, InitializeSourcelinkTool());

            if (failedFiles.Count > 0)
            {
                OutputHelper.WriteLine($"Sourcelink verification failed for the following files:");
                foreach (string file in failedFiles)
                {
                    OutputHelper.WriteLine(file);
                }
            }

            Assert.True(failedFiles.Count == 0);
        }
        finally
        {
            Directory.Delete(SourcelinkRoot, true);
        }
    }

    /// <summary>
    /// Initializes sourcelink tool.
    /// Extracts the dotnet-sourcelink tool package from PSB arhive.
    /// </summary>
    /// <returns>Path to sourcelink tool binary.</returns>
    private string InitializeSourcelinkTool()
    {
        Assert.NotNull(Config.SourceBuiltArtifactsPath);
        
        const string SourcelinkToolPackageNamePattern = "dotnet-sourcelink*nupkg";
        const string SourcelinkToolBinaryFilename = "dotnet-sourcelink.dll";

        string toolPackageDir = Directory.CreateDirectory(Path.Combine(SourcelinkRoot, "sourcelink-tool")).FullName;
        Utilities.ExtractTarball(Config.SourceBuiltArtifactsPath, toolPackageDir, SourcelinkToolPackageNamePattern);

        string extractedToolPath = Directory.CreateDirectory(Path.Combine(toolPackageDir, "extracted")).FullName;
        Utilities.ExtractNupkg(Utilities.GetFile(toolPackageDir, SourcelinkToolPackageNamePattern), extractedToolPath);

        return Utilities.GetFile(extractedToolPath, SourcelinkToolBinaryFilename);
    }

    private IList<string> ValidateSymbols(string path, string sourcelinkToolPath)
    {
        Assert.True(Directory.Exists(path), $"Path, with symbol files to validate, does not exist: {path}");

        var failedFiles = new ConcurrentBag<string>();

        IEnumerable<string> allFiles = Directory.GetFiles(path, "*.pdb", SearchOption.AllDirectories);
        Parallel.ForEach(allFiles, file =>
        {
            (Process Process, string StdOut, string StdErr) executeResult = ExecuteHelper.ExecuteProcess(
                DotNetHelper.DotNetPath,
                $"{sourcelinkToolPath} test --offline {file}",
                OutputHelper,
                logOutput: false,
                excludeInfo: true, // Exclude info messages, as there can be 1,000+ processes
                millisecondTimeout: 60000,
                configureCallback: (process) => DotNetHelper.ConfigureProcess(process, null));

            if (executeResult.Process.ExitCode != 0)
            {
                failedFiles.Add(file);
            }
        });

        Assert.True(allFiles.Count() > 0, $"Did not find any symbols for sourcelink verification in {path}");

        return failedFiles.ToList();
    }
}
