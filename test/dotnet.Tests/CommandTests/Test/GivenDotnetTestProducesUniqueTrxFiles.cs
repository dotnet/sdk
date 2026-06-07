// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.Commands.Test;
using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;
using ExitCodes = Microsoft.NET.TestFramework.ExitCode;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    /// <summary>
    /// End-to-end tests for the SDK's behavior around the Microsoft.Testing.Platform TRX report
    /// extension when multiple modules are involved. Covers the fix for
    /// https://github.com/microsoft/testfx/issues/7345.
    /// </summary>
    public class GivenDotnetTestProducesUniqueTrxFiles : SdkTest
    {
        public GivenDotnetTestProducesUniqueTrxFiles(ITestOutputHelper log) : base(log)
        {
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void MultipleTestModulesWithDefaultTrxFilename_ProducesUniqueFilesWithoutOverwriteWarning(string configuration)
        {
            TestAsset testInstance = TestAssetsManager
                .CopyTestAsset("MultiTestProjectSolutionWithTrxReport", Guid.NewGuid().ToString())
                .WithSource();

            string resultsDirectory = Path.Combine(testInstance.Path, "trx-out");
            Directory.CreateDirectory(resultsDirectory);

            // No --report-trx-filename: the SDK is responsible for injecting a unique-per-module
            // default name. It embeds a timestamp so that re-runs don't overwrite previous results
            // and don't trigger MTP's "Trx file ... already exists and will be overwritten" warning.
            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("-c", configuration,
                    "--report-trx",
                    "--results-directory", resultsDirectory);

            result.ExitCode.Should().Be(ExitCodes.Success);

            string[] trxFiles = Directory.GetFiles(resultsDirectory, "*.trx", SearchOption.AllDirectories);
            trxFiles.Should().HaveCount(2, $"both modules should produce their own TRX. Actual: {string.Join(", ", trxFiles)}");

            string tfm = ToolsetInfo.CurrentTargetFramework;
            // <asm>_<tfm>_<yyyy-MM-dd>_<HH-mm-ss.fffffff>.trx
            string testProjectPattern = $@"^TestProject_{Regex.Escape(tfm)}_\d{{4}}-\d{{2}}-\d{{2}}_\d{{2}}-\d{{2}}-\d{{2}}\.\d{{7}}\.trx$";
            string otherProjectPattern = $@"^OtherTestProject_{Regex.Escape(tfm)}_\d{{4}}-\d{{2}}-\d{{2}}_\d{{2}}-\d{{2}}-\d{{2}}\.\d{{7}}\.trx$";

            var fileNames = trxFiles.Select(f => Path.GetFileName(f)!).ToArray();
            fileNames.Should().Contain(f => Regex.IsMatch(f, testProjectPattern),
                $"the SDK should inject TestProject_<tfm>_<timestamp>.trx as the default name. Actual: {string.Join(", ", fileNames)}");
            fileNames.Should().Contain(f => Regex.IsMatch(f, otherProjectPattern),
                $"the SDK should inject OtherTestProject_<tfm>_<timestamp>.trx as the default name. Actual: {string.Join(", ", fileNames)}");

            if (!SdkTestContext.IsLocalized())
            {
                // MTP's "Trx file '{0}' already exists and will be overwritten." warning must not
                // appear: each injected filename is unique because it includes the module name and a timestamp.
                result.StdOut.Should().NotContain("will be overwritten");
            }
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void MultipleTestModulesWithExplicitTrxFilename_SdkDoesNotRewriteUserFilename(string configuration)
        {
            // Regression guard: when the user explicitly names the TRX file, the SDK must NOT
            // rewrite that name (e.g. by appending `_<asm>_<tfm>` per module). The user's choice
            // is forwarded to Microsoft.Testing.Platform verbatim; MTP decides what happens next
            // (including emitting its own overwrite warning on collisions).
            TestAsset testInstance = TestAssetsManager
                .CopyTestAsset("MultiTestProjectSolutionWithTrxReport", Guid.NewGuid().ToString())
                .WithSource();

            string resultsDirectory = Path.Combine(testInstance.Path, "trx-out");
            Directory.CreateDirectory(resultsDirectory);

            new DotnetTestCommand(Log, disableNewOutput: false)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("-c", configuration,
                    "--report-trx",
                    "--report-trx-filename", "results.trx",
                    "--results-directory", resultsDirectory);

            // We don't assert on the exit code or on how MTP resolves the per-module collision:
            // those are MTP concerns. We only assert that the SDK did not invent suffixed file
            // names like `results_TestProject_<tfm>.trx`.
            string[] trxFiles = Directory.GetFiles(resultsDirectory, "*.trx", SearchOption.AllDirectories);
            var fileNames = trxFiles.Select(f => Path.GetFileName(f)!).ToArray();

            fileNames.Should().NotContain(f => f.Contains("_TestProject", StringComparison.Ordinal)
                                            || f.Contains("_OtherTestProject", StringComparison.Ordinal),
                $"the SDK must forward the user-supplied filename verbatim. Actual: {string.Join(", ", fileNames)}");
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void SingleTestModuleWithExplicitTrxFilename_KeepsFilenameVerbatim(string configuration)
        {
            // Regression guard: when only one test module is being run, the SDK must NOT rewrite
            // the explicit `--report-trx-filename` value. The user gets exactly the file they asked for.
            TestAsset testInstance = TestAssetsManager
                .CopyTestAsset("MultiTestProjectSolutionWithTrxReport", Guid.NewGuid().ToString())
                .WithSource();

            string resultsDirectory = Path.Combine(testInstance.Path, "trx-out");
            Directory.CreateDirectory(resultsDirectory);

            string testProjectPath = Path.Combine("TestProject", "TestProject.csproj");

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("--project", testProjectPath,
                    "-c", configuration,
                    "--report-trx",
                    "--report-trx-filename", "results.trx",
                    "--results-directory", resultsDirectory);

            result.ExitCode.Should().Be(ExitCodes.Success);

            string[] trxFiles = Directory.GetFiles(resultsDirectory, "*.trx", SearchOption.AllDirectories);
            trxFiles.Should().HaveCount(1, $"single-module runs should produce exactly one TRX file. Actual: {string.Join(", ", trxFiles)}");

            var fileNames = trxFiles.Select(f => Path.GetFileName(f)!).ToArray();
            fileNames.Should().ContainSingle().Which.Should().Be("results.trx",
                "single-module runs must keep the user-supplied filename verbatim - no `_<asm>_<tfm>` suffix.");
        }
    }
}
