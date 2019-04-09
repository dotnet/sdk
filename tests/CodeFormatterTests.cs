// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Tools.Tests.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Tools.Tests
{
    public class CodeFormatterTests : IClassFixture<MSBuildFixture>, IClassFixture<SolutionPathFixture>
    {
        private const string FormattedProjectPath = "tests/projects/for_code_formatter/formatted_project";
        private const string FormattedProjectFilePath = FormattedProjectPath + "/formatted_project.csproj";
        private const string FormattedSolutionFilePath = "tests/projects/for_code_formatter/formatted_solution/formatted_solution.sln";

        private const string UnformattedProjectPath = "tests/projects/for_code_formatter/unformatted_project";
        private const string UnformattedProjectFilePath = UnformattedProjectPath + "/unformatted_project.csproj";
        private const string UnformattedProgramFilePath = UnformattedProjectPath + "/program.cs";
        private const string UnformattedSolutionFilePath = "tests/projects/for_code_formatter/unformatted_solution/unformatted_solution.sln";

        private const string FSharpProjectPath = "tests/projects/for_code_formatter/fsharp_project";
        private const string FSharpProjectFilePath = FSharpProjectPath + "/fsharp_project.fsproj";

        private static IEnumerable<string> EmptyFilesToFormat => Array.Empty<string>();

        public CodeFormatterTests(MSBuildFixture msBuildFixture, SolutionPathFixture solutionPathFixture)
        {
            msBuildFixture.RegisterInstance();
            solutionPathFixture.SetCurrentDirectory();
        }

        [Fact]
        public async Task NoFilesFormattedInFormattedProject()
        {
            await TestFormatWorkspaceAsync(
                FormattedProjectFilePath,
                EmptyFilesToFormat,
                expectedExitCode: 0,
                expectedFilesFormatted: 0,
                expectedFileCount: 3);
        }

        [Fact]
        public async Task NoFilesFormattedInFormattedSolution()
        {
            await TestFormatWorkspaceAsync(
                FormattedSolutionFilePath,
                EmptyFilesToFormat,
                expectedExitCode: 0,
                expectedFilesFormatted: 0,
                expectedFileCount: 3);
        }

        [Fact]
        public async Task FilesFormattedInUnformattedProject()
        {
            await TestFormatWorkspaceAsync(
                UnformattedProjectFilePath,
                EmptyFilesToFormat,
                expectedExitCode: 0,
                expectedFilesFormatted: 2,
                expectedFileCount: 4);
        }

        [Fact]
        public async Task FilesFormattedInUnformattedSolution()
        {
            await TestFormatWorkspaceAsync(
                UnformattedSolutionFilePath,
                EmptyFilesToFormat,
                expectedExitCode: 0,
                expectedFilesFormatted: 2,
                expectedFileCount: 4);
        }

        [Fact]
        public async Task FSharpProjectsDoNotCreateException()
        {
            var log = await TestFormatWorkspaceAsync(
                FSharpProjectFilePath,
                EmptyFilesToFormat,
                expectedExitCode: 1,
                expectedFilesFormatted: 0,
                expectedFileCount: 0);

            var pattern = string.Format(Resources.Could_not_format_0_Format_currently_supports_only_CSharp_and_Visual_Basic_projects, "(.*)");
            var match = new Regex(pattern, RegexOptions.Multiline).Match(log);

            Assert.True(match.Success, log);
            Assert.Equal(match.Groups[1].Value, Path.GetFullPath(FSharpProjectFilePath));
        }

        [Fact]
        public async Task OnlyFormatFilesFromList()
        {
            var filesToFormat = new[] { UnformattedProgramFilePath };

            await TestFormatWorkspaceAsync(
                UnformattedProjectFilePath,
                filesToFormat,
                expectedExitCode: 0,
                expectedFilesFormatted: 1,
                expectedFileCount: 4);
        }

        [Fact]
        public async Task NoFilesFormattedWhenNotInList()
        {
            var files = new[] { Path.Combine(UnformattedProjectPath, "does_not_exist.cs") };

            await TestFormatWorkspaceAsync(
                UnformattedProjectFilePath,
                files,
                expectedExitCode: 0,
                expectedFilesFormatted: 0,
                expectedFileCount: 4);
        }

        [Fact]
        public async Task OnlyLogFormattedFiles()
        {
            var files = new[] { UnformattedProgramFilePath };

            var log = await TestFormatWorkspaceAsync(
                UnformattedSolutionFilePath,
                files,
                expectedExitCode: 0,
                expectedFilesFormatted: 1,
                expectedFileCount: 4);

            var pattern = string.Format(Resources.Formatted_code_file_0, @"(.*)");
            var match = new Regex(pattern, RegexOptions.Multiline).Match(log);

            Assert.True(match.Success, log);
            Assert.Equal("Program.cs", match.Groups[1].Value);
        }

        public async Task<string> TestFormatWorkspaceAsync(string solutionOrProjectPath, IEnumerable<string> files, int expectedExitCode, int expectedFilesFormatted, int expectedFileCount)
        {
            var workspacePath = Path.GetFullPath(solutionOrProjectPath);
            var isSolution = workspacePath.EndsWith(".sln");
            var filesToFormat = files.Select(Path.GetFullPath).ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);

            var logger = new TestLogger();
            var formatResult = await CodeFormatter.FormatWorkspaceAsync(workspacePath, isSolution, logAllWorkspaceWarnings: false, saveFormattedFiles: false, filesToFormat, logger, CancellationToken.None);

            Assert.Equal(expectedExitCode, formatResult.ExitCode);
            Assert.Equal(expectedFilesFormatted, formatResult.FilesFormatted);
            Assert.Equal(expectedFileCount, formatResult.FileCount);

            return logger.GetLog();
        }
    }
}
