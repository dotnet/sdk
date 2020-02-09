using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Perf
{
    [SimpleJob(RuntimeMoniker.NetCoreApp21)]
    public class FormattedFiles
    {
        private const string UnformattedProjectPath = "tests/projects/for_code_formatter/unformatted_project";
        private const string UnformattedProjectFilePath = UnformattedProjectPath + "/unformatted_project.csproj";
        private const string UnformattedSolutionFilePath = "tests/projects/for_code_formatter/unformatted_solution/unformatted_solution.sln";
        private static EmptyLogger EmptyLogger = new EmptyLogger();

        [IterationSetup]
        public void NoFilesFormattedSetup()
        {
            MSBuildRegister.RegisterInstance();
            SolutionPathSetter.SetCurrentDirectory();
        }

        [Benchmark(Description = "Whitespace Formatting (folder)")]
        public void FilesFormattedFolder()
        {
            var (workspacePath, workspaceType) = WorkspacePathHelper.GetWorkspaceInfo(UnformattedProjectPath);
            var options = new FormatOptions(
                workspacePath,
                workspaceType,
                LogLevel.Error,
                saveFormattedFiles: false,
                changesAreErrors: false,
                ImmutableHashSet<string>.Empty,
                ImmutableHashSet<string>.Empty,
                reportPath: string.Empty);
            _ = CodeFormatter.FormatWorkspaceAsync(options, EmptyLogger, default).GetAwaiter().GetResult();
        }

        [Benchmark(Description = "Whitespace Formatting (project)")]
        public void FilesFormattedProject()
        {
            var (workspacePath, workspaceType) = WorkspacePathHelper.GetWorkspaceInfo(UnformattedProjectFilePath);
            var options = new FormatOptions(
                workspacePath,
                workspaceType,
                LogLevel.Error,
                saveFormattedFiles: false,
                changesAreErrors: false,
                ImmutableHashSet<string>.Empty,
                ImmutableHashSet<string>.Empty,
                reportPath: string.Empty);
            _ = CodeFormatter.FormatWorkspaceAsync(options, EmptyLogger, default).GetAwaiter().GetResult();
        }

        [Benchmark(Description = "Whitespace Formatting (solution)")]
        public void FilesFormattedSolution()
        {
            var (workspacePath, workspaceType) = WorkspacePathHelper.GetWorkspaceInfo(UnformattedSolutionFilePath);
            var options = new FormatOptions(
                workspacePath,
                workspaceType,
                LogLevel.Error,
                saveFormattedFiles: false,
                changesAreErrors: false,
                ImmutableHashSet<string>.Empty,
                ImmutableHashSet<string>.Empty,
                reportPath: string.Empty);
            _ = CodeFormatter.FormatWorkspaceAsync(options, EmptyLogger, default).GetAwaiter().GetResult();
        }

        [IterationCleanup]
        public void NoFilesFormattedCleanup() => SolutionPathSetter.UnsetCurrentDirectory();
    }
}
