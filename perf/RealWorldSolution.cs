// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using Microsoft.CodeAnalysis.Tools.Utilities;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Perf
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
            MSBuildRegister.RegisterInstance();
            SolutionPathSetter.SetCurrentDirectory();
        }

        [Benchmark(Description = "Formatting Solution")]
        public void FilesFormattedSolution()
        {
            var (workspacePath, workspaceType) = WorkspacePathHelper.GetWorkspaceInfo(UnformattedSolutionFilePath);
            var options = new FormatOptions(
                workspacePath,
                workspaceType,
                noRestore: false,
                LogLevel.Error,
                fixCategory: FixCategory.Whitespace,
                codeStyleSeverity: DiagnosticSeverity.Error,
                analyzerSeverity: DiagnosticSeverity.Error,
                diagnostics: ImmutableHashSet<string>.Empty,
                saveFormattedFiles: false,
                changesAreErrors: false,
                AllFileMatcher,
                reportPath: string.Empty,
                includeGeneratedFiles: false);
            _ = CodeFormatter.FormatWorkspaceAsync(options, EmptyLogger, default).GetAwaiter().GetResult();
        }

        [Benchmark(Description = "Formatting Folder", Baseline = true)]
        public void FilesFormattedFolder()
        {
            var (workspacePath, workspaceType) = WorkspacePathHelper.GetWorkspaceInfo(UnformattedFolderFilePath);
            var options = new FormatOptions(
                workspacePath,
                workspaceType,
                noRestore: false,
                LogLevel.Error,
                fixCategory: FixCategory.Whitespace,
                codeStyleSeverity: DiagnosticSeverity.Error,
                analyzerSeverity: DiagnosticSeverity.Error,
                diagnostics: ImmutableHashSet<string>.Empty,
                saveFormattedFiles: false,
                changesAreErrors: false,
                AllFileMatcher,
                reportPath: string.Empty,
                includeGeneratedFiles: false);
            _ = CodeFormatter.FormatWorkspaceAsync(options, EmptyLogger, default).GetAwaiter().GetResult();
        }

        [IterationCleanup]
        public void RealWorldSolutionCleanup() => SolutionPathSetter.UnsetCurrentDirectory();

        private class RealWorldConfig : ManualConfig
        {
            public RealWorldConfig()
            {
                var job = Job.Dry
                    .WithPlatform(BenchmarkDotNet.Environments.Platform.X64)
                    .WithRuntime(CoreRuntime.Core21)
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
