// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.Commands.Test;
using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;
using ExitCodes = Microsoft.NET.TestFramework.ExitCode;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    /// <summary>
    /// End-to-end tests for the SDK's TRX filename disambiguation across multiple test modules.
    /// Covers the fix for https://github.com/microsoft/testfx/issues/7345 where
    /// `dotnet test --report-trx-filename foo.trx` against a solution with multiple test
    /// modules used to make each module overwrite the others' TRX output.
    /// </summary>
    public class GivenDotnetTestProducesUniqueTrxFiles : SdkTest
    {
        public GivenDotnetTestProducesUniqueTrxFiles(ITestOutputHelper log) : base(log)
        {
        }

        [InlineData(TestingConstants.Debug)]
        [InlineData(TestingConstants.Release)]
        [Theory]
        public void MultipleTestModulesWithExplicitTrxFilename_ProducesOneTrxPerModule(string configuration)
        {
            TestAsset testInstance = TestAssetsManager
                .CopyTestAsset("MultiTestProjectSolutionWithTrxReport", Guid.NewGuid().ToString())
                .WithSource();

            string resultsDirectory = Path.Combine(testInstance.Path, "trx-out");
            Directory.CreateDirectory(resultsDirectory);

            CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("-c", configuration,
                    "--report-trx",
                    "--report-trx-filename", "results.trx",
                    "--results-directory", resultsDirectory);

            result.ExitCode.Should().Be(ExitCodes.Success);

            // The SDK should have rewritten the explicit filename so each module's run lands in a
            // unique TRX file. Without the fix the two modules would race to write to results.trx.
            string[] trxFiles = Directory.GetFiles(resultsDirectory, "*.trx", SearchOption.AllDirectories);
            trxFiles.Should().HaveCount(2, $"both modules should produce their own TRX. Actual: {string.Join(", ", trxFiles)}");

            var fileNames = trxFiles.Select(f => Path.GetFileName(f)!).ToArray();
            string tfm = ToolsetInfo.CurrentTargetFramework;
            fileNames.Should().Contain($"results_TestProject_{tfm}.trx");
            fileNames.Should().Contain($"results_OtherTestProject_{tfm}.trx");

            // The literal filename the user passed must NOT exist as-is.
            fileNames.Should().NotContain("results.trx");
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
            // default name. It also embeds a timestamp so that re-runs don't overwrite previous
            // results and don't trigger MTP's "Trx file ... already exists and will be overwritten" warning.
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
