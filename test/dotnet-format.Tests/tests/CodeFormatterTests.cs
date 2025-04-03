// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

#nullable disable

using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.Tools.Tests.Utilities;
using Microsoft.CodeAnalysis.Tools.Tests.XUnit;
using Microsoft.CodeAnalysis.Tools.Utilities;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Tests
{
    public class CodeFormatterTests
    {
        private static readonly string s_formattedProjectPath = Path.Combine("for_code_formatter", "formatted_project");
        private static readonly string s_formattedProjectFilePath = Path.Combine(s_formattedProjectPath, "formatted_project.csproj");
        private static readonly string s_formattedSolutionFilePath = Path.Combine("for_code_formatter", "formatted_solution", "formatted_solution.sln");

        private static readonly string s_unformattedProjectPath = Path.Combine("for_code_formatter", "unformatted_project");
        private static readonly string s_unformattedProjectFilePath = Path.Combine(s_unformattedProjectPath, "unformatted_project.csproj");
        private static readonly string s_unformattedProgramFilePath = Path.Combine(s_unformattedProjectPath, "program.cs");
        private static readonly string s_unformattedSolutionFilePath = Path.Combine("for_code_formatter", "unformatted_solution", "unformatted_solution.sln");

        private static readonly string s_fSharpProjectPath = Path.Combine("for_code_formatter", "fsharp_project");
        private static readonly string s_fSharpProjectFilePath = Path.Combine(s_fSharpProjectPath, "fsharp_project.fsproj");

        private static readonly string s_generatedProjectPath = Path.Combine("for_code_formatter", "generated_project");
        private static readonly string s_generatedProjectFilePath = Path.Combine(s_generatedProjectPath, "generated_project.csproj");

        private static readonly string s_codeStyleSolutionPath = Path.Combine("for_code_formatter", "codestyle_solution");
        private static readonly string s_codeStyleSolutionFilePath = Path.Combine(s_codeStyleSolutionPath, "codestyle_solution.sln");

        private static readonly string s_codeStyleSolutionFilterFilePath = Path.Combine(s_codeStyleSolutionPath, "codestyle_solution_filter.slnf");

        private static readonly string s_analyzersSolutionPath = Path.Combine("for_code_formatter", "analyzers_solution");
        private static readonly string s_analyzersSolutionFilePath = Path.Combine(s_analyzersSolutionPath, "analyzers_solution.sln");

        private static readonly string s_generatorSolutionPath = Path.Combine("for_code_formatter", "generator_solution");
        private static readonly string s_generatorSolutionFileName = "generator_solution.sln";

        private static string[] EmptyFilesList => Array.Empty<string>();

        private Regex FindFormattingLogLine => new Regex(@"((.*)\(\d+,\d+\): (.*))\r|((.*)\(\d+,\d+\): (.*))");

        private readonly ITestOutputHelper _output;

        public CodeFormatterTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [MSBuildFact]
        public async Task NoFilesFormattedInFormattedProject()
        {
            await TestFormatWorkspaceAsync(
                s_formattedProjectFilePath,
                include: EmptyFilesList,
                exclude: EmptyFilesList,
                includeGenerated: false,
                expectedExitCode: 0,
                expectedFilesFormatted: 0,
                expectedFileCount: 3);
        }

        [MSBuildFact]
        public async Task NoFilesFormattedInFormattedSolution()
        {
            await TestFormatWorkspaceAsync(
                s_formattedSolutionFilePath,
                include: EmptyFilesList,
                exclude: EmptyFilesList,
                includeGenerated: false,
                expectedExitCode: 0,
                expectedFilesFormatted: 0,
                expectedFileCount: 3);
        }

        [MSBuildFact]
        public async Task FilesFormattedInUnformattedProject()
        {
            await TestFormatWorkspaceAsync(
                s_unformattedProjectFilePath,
                include: EmptyFilesList,
                exclude: EmptyFilesList,
                includeGenerated: false,
                expectedExitCode: 0,
                expectedFilesFormatted: 2,
                expectedFileCount: 6);
        }

        [MSBuildFact]
        public async Task NoFilesFormattedInUnformattedProjectWhenFixingCodeStyle()
        {
            await TestFormatWorkspaceAsync(
                s_unformattedProjectFilePath,
                include: EmptyFilesList,
                exclude: EmptyFilesList,
                includeGenerated: false,
                fixCategory: FixCategory.CodeStyle,
                codeStyleSeverity: DiagnosticSeverity.Error,
                expectedExitCode: 0,
                expectedFilesFormatted: 0,
                expectedFileCount: 6);
        }

        [MSBuildFact]
        public async Task GeneratedFilesFormattedInUnformattedProject()
        {
            var log = await TestFormatWorkspaceAsync(
                s_unformattedProjectFilePath,
                include: EmptyFilesList,
                exclude: EmptyFilesList,
                includeGenerated: true,
                expectedExitCode: 0,
                expectedFilesFormatted: 5,
                expectedFileCount: 6);

            var logLines = log.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.Contains(logLines, line => line.Contains("unformatted_project.AssemblyInfo.cs"));
            Assert.Contains(logLines, line => line.Contains("NETCoreApp,Version=v3.1.AssemblyAttributes.cs"));
        }

        [MSBuildFact]
        public async Task FilesFormattedInUnformattedSolution()
        {
            await TestFormatWorkspaceAsync(
                s_unformattedSolutionFilePath,
                include: EmptyFilesList,
                exclude: EmptyFilesList,
                includeGenerated: false,
                expectedExitCode: 0,
                expectedFilesFormatted: 2,
                expectedFileCount: 6);
        }

        [MSBuildFact]
        public async Task FilesFormattedInUnformattedProjectFolder()
        {
            // Since the code files are beneath the project folder, files are found and formatted.
            await TestFormatWorkspaceAsync(
                Path.GetDirectoryName(s_unformattedProjectFilePath),
                include: EmptyFilesList,
                exclude: EmptyFilesList,
                includeGenerated: false,
                expectedExitCode: 0,
                expectedFilesFormatted: 2,
                expectedFileCount: 6);
        }

        [MSBuildFact]
        public async Task NoFilesFormattedInUnformattedSolutionFolder()
        {
            // Since the code files are outside the solution folder, no files are found or formatted.
            await TestFormatWorkspaceAsync(
                Path.GetDirectoryName(s_unformattedSolutionFilePath),
                include: EmptyFilesList,
                exclude: EmptyFilesList,
                includeGenerated: false,
                expectedExitCode: 0,
                expectedFilesFormatted: 0,
                expectedFileCount: 0);
        }

        [MSBuildFact]
        public async Task FSharpProjectsDoNotCreateException()
        {
            var log = await TestFormatWorkspaceAsync(
                s_fSharpProjectFilePath,
                include: EmptyFilesList,
                exclude: EmptyFilesList,
                includeGenerated: false,
                expectedExitCode: 1,
                expectedFilesFormatted: 0,
                expectedFileCount: 0);

            var pattern = string.Format(Resources.Could_not_format_0_Format_currently_supports_only_CSharp_and_Visual_Basic_projects, "(.*)");
            var match = new Regex(pattern, RegexOptions.Multiline).Match(log);

            Assert.True(match.Success, log);
            Assert.EndsWith(s_fSharpProjectFilePath, match.Groups[1].Value);
        }

        [MSBuildFact]
        public async Task OnlyFormatPathsFromList()
        {
            // To match a folder pattern it needs to end with a directory separator.
            var include = new[] { s_unformattedProjectPath + Path.DirectorySeparatorChar };

            await TestFormatWorkspaceAsync(
                s_unformattedProjectFilePath,
                include,
                exclude: EmptyFilesList,
                includeGenerated: false,
                expectedExitCode: 0,
                expectedFilesFormatted: 2,
                expectedFileCount: 6);
        }

        [MSBuildFact]
        public async Task OnlyFormatFilesFromList()
        {
            var include = new[] { s_unformattedProgramFilePath };

            await TestFormatWorkspaceAsync(
                s_unformattedProjectFilePath,
                include,
                exclude: EmptyFilesList,
                includeGenerated: false,
                expectedExitCode: 0,
                expectedFilesFormatted: 1,
                expectedFileCount: 6);
        }

        [MSBuildFact]
        public async Task NoFilesFormattedWhenNotInList()
        {
            var include = new[] { Path.Combine(s_unformattedProjectPath, "does_not_exist.cs") };

            await TestFormatWorkspaceAsync(
                s_unformattedProjectFilePath,
                include,
                exclude: EmptyFilesList,
                includeGenerated: false,
                expectedExitCode: 0,
                expectedFilesFormatted: 0,
                expectedFileCount: 6);
        }

        [MSBuildFact]
        public async Task OnlyLogFormattedFiles()
        {
            var include = new[] { s_unformattedProgramFilePath };

            var log = await TestFormatWorkspaceAsync(
                s_unformattedSolutionFilePath,
                include,
                exclude: EmptyFilesList,
                includeGenerated: false,
                expectedExitCode: 0,
                expectedFilesFormatted: 1,
                expectedFileCount: 6);

            var pattern = string.Format(Resources.Formatted_code_file_0, @"(.*)");
            var match = new Regex(pattern, RegexOptions.Multiline).Match(log);

            Assert.True(match.Success, log);
            Assert.EndsWith("Program.cs", match.Groups[1].Value);
        }

        [MSBuildFact]
        public async Task FormatLocationsLoggedInUnformattedProject()
        {
            var log = await TestFormatWorkspaceAsync(
                s_unformattedProjectFilePath,
                include: EmptyFilesList,
                exclude: EmptyFilesList,
                includeGenerated: false,
                expectedExitCode: 0,
                expectedFilesFormatted: 2,
                expectedFileCount: 6);

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

        [MSBuildFact]
        public async Task FormatLocationsNotLoggedInFormattedProject()
        {
            var log = await TestFormatWorkspaceAsync(
                s_formattedProjectFilePath,
                include: EmptyFilesList,
                exclude: EmptyFilesList,
                includeGenerated: false,
                expectedExitCode: 0,
                expectedFilesFormatted: 0,
                expectedFileCount: 3);

            var formatLocations = log.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => FindFormattingLogLine.Match(line).Success);

            Assert.Empty(formatLocations);
        }

        [MSBuildFact]
        public async Task LogFilesThatDontMatchExclude()
        {
            var include = new[] { s_unformattedProgramFilePath };

            var log = await TestFormatWorkspaceAsync(
                s_unformattedSolutionFilePath,
                include,
                exclude: EmptyFilesList,
                includeGenerated: false,
                expectedExitCode: 0,
                expectedFilesFormatted: 1,
                expectedFileCount: 6);

            var pattern = string.Format(Resources.Formatted_code_file_0, @"(.*)");
            var match = new Regex(pattern, RegexOptions.Multiline).Match(log);

            Assert.True(match.Success, log);
            Assert.EndsWith("Program.cs", match.Groups[1].Value);
        }

        [MSBuildFact]
        public async Task IgnoreFileWhenListedInExcludeList()
        {
            var include = new[] { s_unformattedProgramFilePath };

            await TestFormatWorkspaceAsync(
                s_unformattedSolutionFilePath,
                include: include,
                exclude: include,
                includeGenerated: false,
                expectedExitCode: 0,
                expectedFilesFormatted: 0,
                expectedFileCount: 6);
        }

        [MSBuildFact]
        public async Task IgnoreFileWhenContainingFolderListedInExcludeList()
        {
            var include = new[] { s_unformattedProgramFilePath };
            var exclude = new[] { s_unformattedProjectPath };

            await TestFormatWorkspaceAsync(
                s_unformattedSolutionFilePath,
                include: include,
                exclude: exclude,
                includeGenerated: false,
                expectedExitCode: 0,
                expectedFilesFormatted: 0,
                expectedFileCount: 6);
        }

        [MSBuildFact]
        public async Task IgnoreAllFileWhenExcludingAllFiles()
        {
            var include = new[] { s_unformattedProgramFilePath };
            var exclude = new[] { "**/*.*" };

            await TestFormatWorkspaceAsync(
                s_unformattedSolutionFilePath,
                include: include,
                exclude: exclude,
                includeGenerated: false,
                expectedExitCode: 0,
                expectedFilesFormatted: 0,
                expectedFileCount: 6);
        }

        [MSBuildFact]
        public async Task NoFilesFormattedInGeneratedProject_WhenNotIncludingGeneratedCode()
        {
            await TestFormatWorkspaceAsync(
                s_generatedProjectFilePath,
                include: EmptyFilesList,
                exclude: EmptyFilesList,
                includeGenerated: false,
                expectedExitCode: 0,
                expectedFilesFormatted: 0,
                expectedFileCount: 3);
        }

        [MSBuildFact]
        public async Task FilesFormattedInGeneratedProject_WhenIncludingGeneratedCode()
        {
            await TestFormatWorkspaceAsync(
                s_generatedProjectFilePath,
                include: EmptyFilesList,
                exclude: EmptyFilesList,
                includeGenerated: true,
                expectedExitCode: 0,
                expectedFilesFormatted: 3,
                expectedFileCount: 3);
        }

        [MSBuildFact]
        public async Task NoFilesFormattedInCodeStyleSolution_WhenNotFixingCodeStyle()
        {
            await TestFormatWorkspaceAsync(
                s_codeStyleSolutionFilePath,
                include: EmptyFilesList,
                exclude: EmptyFilesList,
                includeGenerated: false,
                expectedExitCode: 0,
                expectedFilesFormatted: 0,
                expectedFileCount: 6,
                fixCategory: FixCategory.Whitespace | FixCategory.CodeStyle);
        }

        [MSBuildFact]
        public async Task NoFilesFormattedInCodeStyleSolution_WhenFixingCodeStyleErrors()
        {
            await TestFormatWorkspaceAsync(
                s_codeStyleSolutionFilePath,
                include: EmptyFilesList,
                exclude: EmptyFilesList,
                includeGenerated: false,
                expectedExitCode: 0,
                expectedFilesFormatted: 0,
                expectedFileCount: 6,
                fixCategory: FixCategory.Whitespace | FixCategory.CodeStyle,
                codeStyleSeverity: DiagnosticSeverity.Error);
        }

        [MSBuildFact]
        public async Task FilesFormattedInCodeStyleSolution_WhenFixingCodeStyleWarnings()
        {
            await TestFormatWorkspaceAsync(
                s_codeStyleSolutionFilePath,
                include: EmptyFilesList,
                exclude: EmptyFilesList,
                includeGenerated: false,
                expectedExitCode: 0,
                expectedFilesFormatted: 2,
                expectedFileCount: 6,
                fixCategory: FixCategory.Whitespace | FixCategory.CodeStyle,
                codeStyleSeverity: DiagnosticSeverity.Warning);
        }

        [MSBuildFact]
        public async Task FilesFormattedInCodeStyleSolutionFilter_WhenFixingCodeStyleWarnings()
        {
            var restoreExitCode = await Utilities.DotNetHelper.PerformRestoreAsync(s_codeStyleSolutionFilterFilePath, _output);
            Assert.Equal(0, restoreExitCode);

            await TestFormatWorkspaceAsync(
                s_codeStyleSolutionFilterFilePath,
                include: EmptyFilesList,
                exclude: EmptyFilesList,
                includeGenerated: false,
                expectedExitCode: 0,
                expectedFilesFormatted: 1,
                expectedFileCount: 3,
                fixCategory: FixCategory.Whitespace | FixCategory.CodeStyle,
                codeStyleSeverity: DiagnosticSeverity.Warning);
        }

        [MSBuildFact]
        public async Task NoFilesFormattedInAnalyzersSolution_WhenNotFixingAnalyzers()
        {
            await TestFormatWorkspaceAsync(
                s_analyzersSolutionFilePath,
                include: EmptyFilesList,
                exclude: EmptyFilesList,
                includeGenerated: false,
                expectedExitCode: 0,
                expectedFilesFormatted: 0,
                expectedFileCount: 7,
                fixCategory: FixCategory.Whitespace);
        }

        [MSBuildFact]
        public async Task FilesFormattedInAnalyzersSolution_WhenFixingAnalyzerErrors()
        {
            await TestFormatWorkspaceAsync(
                s_analyzersSolutionFilePath,
                include: EmptyFilesList,
                exclude: EmptyFilesList,
                includeGenerated: false,
                expectedExitCode: 0,
                expectedFilesFormatted: 1,
                expectedFileCount: 7,
                fixCategory: FixCategory.Whitespace | FixCategory.Analyzers,
                analyzerSeverity: DiagnosticSeverity.Error);
        }

        [MSBuildFact]
        public async Task AdditionalDocumentsSavedInAnalyzersSolution_WhenFixingAnalyzerErrors()
        {
            // Copy solution to temp folder so we can write changes to disk.
            var solutionPath = CopyToTempFolder(s_analyzersSolutionPath);

            try
            {
                // Fix PublicAPI analyzer diagnostics.
                await TestFormatWorkspaceAsync(
                    Path.Combine(solutionPath, "library", "library.csproj"),
                    include: EmptyFilesList,
                    exclude: EmptyFilesList,
                    includeGenerated: false,
                    expectedExitCode: 0,
                    expectedFilesFormatted: 1,
                    expectedFileCount: 3,
                    fixCategory: FixCategory.Analyzers,
                    analyzerSeverity: DiagnosticSeverity.Warning,
                    diagnostics: new[] { "RS0016" },
                    saveFormattedFiles: true);

                // Verify that changes were persisted to disk.
                var unshippedPublicApi = File.ReadAllText(Path.Combine(solutionPath, "library", "PublicAPI.Unshipped.txt"));
                Assert.NotEqual(string.Empty, unshippedPublicApi);
            }
            finally
            {
                // Cleanup
                Directory.Delete(solutionPath, true);
            }
        }

        [MSBuildFact]
        public async Task GeneratorSolution_NoDiagnosticsReported_WhenNotIncludingGenerated()
        {
            // Copy solution to temp folder so we can write changes to disk.
            var solutionPath = CopyToTempFolder(s_generatorSolutionPath);

            try
            {
                var solutionFilePath = Path.Combine(solutionPath, s_generatorSolutionFileName);

                var buildExitCode = await Utilities.DotNetHelper.PerformBuildAsync(solutionFilePath, _output);
                Assert.Equal(0, buildExitCode);

                // Fix PublicAPI analyzer diagnostics.
                await TestFormatWorkspaceAsync(
                    solutionFilePath,
                    include: EmptyFilesList,
                    exclude: EmptyFilesList,
                    includeGenerated: false,
                    expectedExitCode: 0,
                    expectedFilesFormatted: 0,
                    expectedFileCount: 7,
                    fixCategory: FixCategory.Analyzers,
                    analyzerSeverity: DiagnosticSeverity.Warning,
                    diagnostics: new[] { "RS0016" },
                    saveFormattedFiles: true);

                // Verify that changes were persisted to disk.
                var unshippedPublicApi = File.ReadAllText(Path.Combine(solutionPath, "console_app", "PublicAPI.Unshipped.txt"));
                Assert.Equal(string.Empty, unshippedPublicApi);
            }
            finally
            {
                try
                {
                    Directory.Delete(solutionPath, true);
                }
                catch (UnauthorizedAccessException)
                {
                    // The Windows the generator library may still be locked
                }
            }
        }

        [MSBuildFact]
        public async Task GeneratorSolution_AdditionalDocumentsUpdated_WhenIncludingGenerated()
        {
            const string ExpectedPublicApi = @"Greeter
Greeter.Greet() -> void
Greeter.Greeter() -> void";

            // Copy solution to temp folder so we can write changes to disk.
            var solutionPath = CopyToTempFolder(s_generatorSolutionPath);

            try
            {
                var solutionFilePath = Path.Combine(solutionPath, s_generatorSolutionFileName);

                var buildExitCode = await Utilities.DotNetHelper.PerformBuildAsync(solutionFilePath, _output);
                Assert.Equal(0, buildExitCode);

                // Fix PublicAPI analyzer diagnostics.
                await TestFormatWorkspaceAsync(
                    solutionFilePath,
                    include: EmptyFilesList,
                    exclude: EmptyFilesList,
                    includeGenerated: true,
                    expectedExitCode: 0,
                    expectedFilesFormatted: 1,
                    expectedFileCount: 8,
                    fixCategory: FixCategory.Analyzers,
                    analyzerSeverity: DiagnosticSeverity.Warning,
                    diagnostics: new[] { "RS0016" },
                    saveFormattedFiles: true);

                // Verify that changes were persisted to disk.
                var unshippedPublicApi = File.ReadAllText(Path.Combine(solutionPath, "console_app", "PublicAPI.Unshipped.txt"));
                Assert.Equal(ExpectedPublicApi, unshippedPublicApi);
            }
            finally
            {
                try
                {
                    Directory.Delete(solutionPath, true);
                }
                catch (UnauthorizedAccessException)
                {
                    // On Windows the generator library may still be locked
                }
            }
        }

        internal async Task<string> TestFormatWorkspaceAsync(
            string workspaceFilePath,
            string[] include,
            string[] exclude,
            bool includeGenerated,
            int expectedExitCode,
            int expectedFilesFormatted,
            int expectedFileCount,
            FixCategory fixCategory = FixCategory.Whitespace,
            DiagnosticSeverity codeStyleSeverity = DiagnosticSeverity.Error,
            DiagnosticSeverity analyzerSeverity = DiagnosticSeverity.Error,
            string[] diagnostics = null,
            bool noRestore = false,
            bool saveFormattedFiles = false)
        {
            var currentDirectory = Environment.CurrentDirectory;
            Environment.CurrentDirectory = TestProjectsPathHelper.GetProjectsDirectory();

            var workspacePath = Path.GetFullPath(workspaceFilePath);

            WorkspaceType workspaceType;
            if (Directory.Exists(workspacePath))
            {
                workspaceType = WorkspaceType.Folder;
            }
            else
            {
                workspaceType = workspacePath.EndsWith("proj")
                    ? WorkspaceType.Project
                    : WorkspaceType.Solution;
            }

            var logger = new TestLogger();
            var msBuildPath = MSBuildRegistrar.RegisterInstance();

            logger.LogTrace(Resources.Using_msbuildexe_located_in_0, msBuildPath);

            var fileMatcher = SourceFileMatcher.CreateMatcher(include, exclude);
            var formatOptions = new FormatOptions(
                workspacePath,
                workspaceType,
                noRestore,
                LogLevel.Trace,
                fixCategory,
                codeStyleSeverity,
                analyzerSeverity,
                diagnostics?.ToImmutableHashSet() ?? ImmutableHashSet<string>.Empty,
                ExcludeDiagnostics: ImmutableHashSet<string>.Empty,
                saveFormattedFiles,
                ChangesAreErrors: false,
                fileMatcher,
                ReportPath: string.Empty,
                IncludeGeneratedFiles: includeGenerated,
                BinaryLogPath: null);
            var formatResult = await CodeFormatter.FormatWorkspaceAsync(formatOptions, logger, CancellationToken.None);
            Environment.CurrentDirectory = currentDirectory;

            var log = logger.GetLog();

            try
            {
                Assert.Equal(expectedExitCode, formatResult.ExitCode);
                Assert.Equal(expectedFilesFormatted, formatResult.FilesFormatted);
                Assert.Equal(expectedFileCount, formatResult.FileCount);
            }
            catch
            {
                _output.WriteLine(log);
                throw;
            }

            return log;
        }

        /// <summary>
        /// Copies the specified folder to the temp folder and returns the path.
        /// </summary>
        private static string CopyToTempFolder(string sourcePath)
        {
            var fullPath = Path.GetFullPath(sourcePath, TestProjectsPathHelper.GetProjectsDirectory());
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            DirectoryCopy(fullPath, tempPath, true);

            return tempPath;

            static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
            {
                // Get the subdirectories for the specified directory.
                var dir = new DirectoryInfo(sourceDirName);

                if (!dir.Exists)
                {
                    throw new DirectoryNotFoundException(
                        "Source directory does not exist or could not be found: "
                        + sourceDirName);
                }

                var dirs = dir.GetDirectories();

                // If the destination directory doesn't exist, create it.
                Directory.CreateDirectory(destDirName);

                // Get the files in the directory and copy them to the new location.
                var files = dir.GetFiles();
                foreach (var file in files)
                {
                    var tempPath = Path.Combine(destDirName, file.Name);
                    file.CopyTo(tempPath, false);
                }

                // If copying subdirectories, copy them and their contents to new location.
                if (copySubDirs)
                {
                    foreach (var subdir in dirs)
                    {
                        var tempPath = Path.Combine(destDirName, subdir.Name);
                        DirectoryCopy(subdir.FullName, tempPath, copySubDirs);
                    }
                }
            }
        }
    }
}
