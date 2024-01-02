// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using Microsoft.CodeAnalysis.Tools.Utilities;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Perf.Real
{
    [Config(typeof(RealWorldConfig))]
    public class RealWorldSolution
    {
        private const string UnformattedFolderFilePath = "temp/project-system/";
        private const string UnformattedSolutionFilePath = UnformattedFolderFilePath + "ProjectSystem.sln";

        private static EmptyLogger EmptyLogger => new EmptyLogger();
        private static SourceFileMatcher AllFileMatcher => SourceFileMatcher.CreateMatcher(Array.Empty<string>(), Array.Empty<string>());

        [IterationSetup]
        public void RealWorldSolutionIterationSetup()
        {
            SolutionPathSetter.SetCurrentDirectory();
            MSBuildRegister.RegisterInstance(Environment.CurrentDirectory);
        }

        [Benchmark(Description = "Formatting Solution")]
        public void FilesFormattedSolution()
        {
            var (workspacePath, workspaceType) = WorkspacePathHelper.GetWorkspaceInfo(UnformattedSolutionFilePath);
            var options = new FormatOptions(
                workspacePath,
                workspaceType,
                NoRestore: false,
                LogLevel.Error,
                FixCategory: FixCategory.Whitespace,
                CodeStyleSeverity: DiagnosticSeverity.Error,
                AnalyzerSeverity: DiagnosticSeverity.Error,
                Diagnostics: ImmutableHashSet<string>.Empty,
                ExcludeDiagnostics: ImmutableHashSet<string>.Empty,
                SaveFormattedFiles: false,
                ChangesAreErrors: false,
                AllFileMatcher,
                ReportPath: string.Empty,
                IncludeGeneratedFiles: false,
                BinaryLogPath: null);
            _ = CodeFormatter.FormatWorkspaceAsync(options, EmptyLogger, default).GetAwaiter().GetResult();
        }

        [Benchmark(Description = "Formatting Folder", Baseline = true)]
        public void FilesFormattedFolder()
        {
            var (workspacePath, workspaceType) = WorkspacePathHelper.GetWorkspaceInfo(UnformattedFolderFilePath);
            var options = new FormatOptions(
                workspacePath,
                workspaceType,
                NoRestore: false,
                LogLevel.Error,
                FixCategory: FixCategory.Whitespace,
                CodeStyleSeverity: DiagnosticSeverity.Error,
                AnalyzerSeverity: DiagnosticSeverity.Error,
                Diagnostics: ImmutableHashSet<string>.Empty,
                ExcludeDiagnostics: ImmutableHashSet<string>.Empty,
                SaveFormattedFiles: false,
                ChangesAreErrors: false,
                AllFileMatcher,
                ReportPath: string.Empty,
                IncludeGeneratedFiles: false,
                BinaryLogPath: null);
            _ = CodeFormatter.FormatWorkspaceAsync(options, EmptyLogger, default).GetAwaiter().GetResult();
        }

        [IterationCleanup]
        public void RealWorldSolutionCleanup() => SolutionPathSetter.UnsetCurrentDirectory();

        private class RealWorldConfig : ManualConfig
        {
            public RealWorldConfig()
            {
                var job = Job.Dry
                    .WithWarmupCount(1)
                    .WithIterationCount(12)
                    .WithOutlierMode(Perfolizer.Mathematics.OutlierDetection.OutlierMode.RemoveAll);
                Add(DefaultConfig.Instance
                    .AddJob(job.AsDefault())
                    .AddDiagnoser(MemoryDiagnoser.Default));
            }
        }
    }
}
