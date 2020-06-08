// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using Microsoft.CodeAnalysis.Tools.Utilities;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Perf
{
    [Config(typeof(RealWorldConfig))]
    public class RealWorldSolution
    {
        private const string UnformattedFolderFilePath = "temp/project-system/";
        private const string UnformattedSolutionFilePath = UnformattedFolderFilePath + "ProjectSystem.sln";

        private static EmptyLogger EmptyLogger => new EmptyLogger();
        private static Matcher AllFileMatcher => SourceFileMatcher.CreateMatcher(Array.Empty<string>(), Array.Empty<string>());

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
                LogLevel.Error,
                fixCodeStyle: false,
                codeStyleSeverity: DiagnosticSeverity.Error,
                fixAnalyzers: false,
                analyerSeverity: DiagnosticSeverity.Error,
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
                LogLevel.Error,
                fixCodeStyle: false,
                codeStyleSeverity: DiagnosticSeverity.Error,
                fixAnalyzers: false,
                analyerSeverity: DiagnosticSeverity.Error,
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
                    .With(BenchmarkDotNet.Environments.Platform.X64)
                    .With(CoreRuntime.Core21)
                    .WithWarmupCount(1)
                    .WithIterationCount(12)
                    .WithOutlierMode(BenchmarkDotNet.Mathematics.OutlierMode.RemoveAll);
                Add(DefaultConfig.Instance
                    .With(job.AsDefault())
                    .With(MemoryDiagnoser.Default));
            }
        }
    }
}
