// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Tools.Tests.Utilities;
using Microsoft.Extensions.Logging;
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

        private Regex FindFormattingLogLine => new Regex(@"((.*)\(\d+,\d+\): (.*))\r|((.*)\(\d+,\d+\): (.*))");

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
                expectedFileCount: 5);
        }

        [Fact]
        public async Task FilesFormattedInUnformattedSolution()
        {
            await TestFormatWorkspaceAsync(
                UnformattedSolutionFilePath,
                EmptyFilesToFormat,
                expectedExitCode: 0,
                expectedFilesFormatted: 2,
                expectedFileCount: 5);
        }


        [Fact]
        public async Task FilesFormattedInUnformattedProjectFolder()
        {
            // Since the code files are beneath the project folder, files are found and formatted.
            await TestFormatWorkspaceAsync(
                Path.GetDirectoryName(UnformattedProjectFilePath),
                EmptyFilesToFormat,
                expectedExitCode: 0,
                expectedFilesFormatted: 2,
                expectedFileCount: 3);
        }

        [Fact]
        public async Task NoFilesFormattedInUnformattedSolutionFolder()
        {
            // Since the code files are outside the solution folder, no files are found or formatted.
            await TestFormatWorkspaceAsync(
                Path.GetDirectoryName(UnformattedSolutionFilePath),
                EmptyFilesToFormat,
                expectedExitCode: 0,
                expectedFilesFormatted: 0,
                expectedFileCount: 0);
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
                expectedFileCount: 5);
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
                expectedFileCount: 5);
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
                expectedFileCount: 5);

            var pattern = string.Format(Resources.Formatted_code_file_0, @"(.*)");
            var match = new Regex(pattern, RegexOptions.Multiline).Match(log);

            Assert.True(match.Success, log);
            Assert.Equal("Program.cs", match.Groups[1].Value);
        }

        [Fact]
        public async Task FormatLocationsLoggedInUnformattedProject()
        {
            var log = await TestFormatWorkspaceAsync(
                UnformattedProjectFilePath,
                EmptyFilesToFormat,
                expectedExitCode: 0,
                expectedFilesFormatted: 2,
                expectedFileCount: 5);

            var formatLocations = log.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => FindFormattingLogLine.Match(line).Success)
                .ToArray();

            var expectedFormatLocations = new[]
            {
                @"other_items\OtherClass.cs(5,3): Fix whitespace formatting.",
                @"other_items\OtherClass.cs(6,3): Fix whitespace formatting.",
                @"other_items\OtherClass.cs(7,5): Fix whitespace formatting.",
                @"other_items\OtherClass.cs(8,5): Fix whitespace formatting.",
                @"other_items\OtherClass.cs(9,7): Fix whitespace formatting.",
                @"other_items\OtherClass.cs(10,5): Fix whitespace formatting.",
                @"other_items\OtherClass.cs(11,3): Fix whitespace formatting.",
                @"Program.cs(5,3): Fix whitespace formatting.",
                @"Program.cs(6,3): Fix whitespace formatting.",
                @"Program.cs(7,5): Fix whitespace formatting.",
                @"Program.cs(8,5): Fix whitespace formatting.",
                @"Program.cs(9,7): Fix whitespace formatting.",
                @"Program.cs(10,5): Fix whitespace formatting.",
                @"Program.cs(11,3): Fix whitespace formatting.",
                @"other_items\OtherClass.cs(12,2): Add final newline.",
                @"Program.cs(12,2): Add final newline.",
            }.Select(path => path.Replace('\\', Path.DirectorySeparatorChar)).ToArray();

            // We can't assert the location of the format message because different platform
            // line endings change the position in the file.
            Assert.Equal(expectedFormatLocations.Length, formatLocations.Length);
            for (var index = 0; index < expectedFormatLocations.Length; index++)
            {
                var expectedParts = FindFormattingLogLine.Match(expectedFormatLocations[index]);
                var formatParts = FindFormattingLogLine.Match(formatLocations[index]);

                // Match filename
                Assert.Equal(expectedParts.Groups[2].Value, formatParts.Groups[2].Value);
                // Match formatter message
                Assert.Equal(expectedParts.Groups[3].Value, formatParts.Groups[3].Value);
            }
        }

        [Fact]
        public async Task FormatLocationsNotLoggedInFormattedProject()
        {
            var log = await TestFormatWorkspaceAsync(
                FormattedProjectFilePath,
                EmptyFilesToFormat,
                expectedExitCode: 0,
                expectedFilesFormatted: 0,
                expectedFileCount: 3);

            var formatLocations = log.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => FindFormattingLogLine.Match(line).Success);

            Assert.Empty(formatLocations);
        }

        public async Task<string> TestFormatWorkspaceAsync(string workspaceFilePath, IEnumerable<string> files, int expectedExitCode, int expectedFilesFormatted, int expectedFileCount)
        {
            var workspacePath = Path.GetFullPath(workspaceFilePath);

            WorkspaceType workspaceType;
            if (Directory.Exists(workspacePath))
            {
                workspaceType = WorkspaceType.Folder;
            }
            else
            {
                workspaceType = workspacePath.EndsWith(".sln")
                    ? WorkspaceType.Solution
                    : WorkspaceType.Project;
            }

            var filesToFormat = files.Select(Path.GetFullPath).ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);

            var logger = new TestLogger();
            var formatOptions = new FormatOptions(
                workspacePath,
                workspaceType,
                LogLevel.Trace,
                saveFormattedFiles: false,
                changesAreErrors: false,
                filesToFormat,
                filesToIgnore: ImmutableHashSet.Create<string>(),
                reportPath: string.Empty);
            var formatResult = await CodeFormatter.FormatWorkspaceAsync(formatOptions, logger, CancellationToken.None);

            Assert.Equal(expectedExitCode, formatResult.ExitCode);
            Assert.Equal(expectedFilesFormatted, formatResult.FilesFormatted);
            Assert.Equal(expectedFileCount, formatResult.FileCount);

            return logger.GetLog();
        }
    }
}
