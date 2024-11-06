// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Diagnostics;
using Microsoft.DotNet.Watcher.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal class BuildEvaluator(DotNetWatchContext context, MSBuildFileSetFactory rootProjectFileSetFactory)
    {
        // File types that require an MSBuild re-evaluation
        private static readonly string[] s_msBuildFileExtensions = new[]
        {
            ".csproj", ".props", ".targets", ".fsproj", ".vbproj", ".vcxproj",
        };

        private static readonly int[] s_msBuildFileExtensionHashes = s_msBuildFileExtensions
            .Select(e => e.GetHashCode(StringComparison.OrdinalIgnoreCase))
            .ToArray();

        private List<(string fileName, DateTime lastWriteTimeUtc)>? _msbuildFileTimestamps;

        // result of the last evaluation, or null if no evaluation has been performed yet.
        private EvaluationResult? _evaluationResult;

        public bool RequiresRevaluation { get; set; }

        public IReadOnlyList<string> GetProcessArguments(int iteration)
        {
            if (!context.EnvironmentOptions.SuppressMSBuildIncrementalism &&
                iteration > 0 &&
                context.RootProjectOptions.Command is "run" or "test")
            {
                if (RequiresRevaluation)
                {
                    context.Reporter.Verbose("Cannot use --no-restore since msbuild project files have changed.");
                }
                else
                {
                    context.Reporter.Verbose("Modifying command to use --no-restore");
                    return [context.RootProjectOptions.Command, "--no-restore", .. context.RootProjectOptions.CommandArguments];
                }
            }

            return [context.RootProjectOptions.Command, .. context.RootProjectOptions.CommandArguments];
        }

        public async ValueTask<EvaluationResult> EvaluateAsync(ChangedFile? changedFile, CancellationToken cancellationToken)
        {
            if (context.EnvironmentOptions.SuppressMSBuildIncrementalism)
            {
                RequiresRevaluation = true;
                return _evaluationResult = await CreateEvaluationResult(cancellationToken);
            }

            if (_evaluationResult == null || RequiresMSBuildRevaluation(changedFile?.Item))
            {
                RequiresRevaluation = true;
            }

            if (RequiresRevaluation)
            {
                context.Reporter.Verbose("Evaluating dotnet-watch file set.");

                var result = await CreateEvaluationResult(cancellationToken);
                _msbuildFileTimestamps = GetMSBuildFileTimeStamps(result);
                return _evaluationResult = result;
            }

            Debug.Assert(_evaluationResult != null);
            return _evaluationResult;
        }

        private async ValueTask<EvaluationResult> CreateEvaluationResult(CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await rootProjectFileSetFactory.TryCreateAsync(cancellationToken);
                if (result != null)
                {
                    return result;
                }

                context.Reporter.Warn("Fix the error to continue or press Ctrl+C to exit.");
                await FileWatcher.WaitForFileChangeAsync(rootProjectFileSetFactory.RootProjectFile, context.Reporter, cancellationToken);
            }
        }

        private bool RequiresMSBuildRevaluation(FileItem? changedFile)
        {
            Debug.Assert(_msbuildFileTimestamps != null);

            if (changedFile != null && IsMsBuildFileExtension(changedFile.Value.FilePath))
            {
                return true;
            }

            // The filewatcher may miss changes to files. For msbuild files, we can verify that they haven't been modified
            // since the previous iteration.
            // We do not have a way to identify renames or new additions that the file watcher did not pick up,
            // without performing an evaluation. We will start off by keeping it simple and comparing the timestamps
            // of known MSBuild files from previous run. This should cover the vast majority of cases.

            foreach (var (file, lastWriteTimeUtc) in _msbuildFileTimestamps)
            {
                if (GetLastWriteTimeUtcSafely(file) != lastWriteTimeUtc)
                {
                    context.Reporter.Verbose($"Re-evaluation needed due to changes in {file}.");

                    return true;
                }
            }

            return false;
        }

        private List<(string fileName, DateTime lastModifiedUtc)> GetMSBuildFileTimeStamps(EvaluationResult result)
        {
            var msbuildFiles = new List<(string fileName, DateTime lastModifiedUtc)>();
            foreach (var (filePath, _) in result.Files)
            {
                if (!string.IsNullOrEmpty(filePath) && IsMsBuildFileExtension(filePath))
                {
                    msbuildFiles.Add((filePath, GetLastWriteTimeUtcSafely(filePath)));
                }
            }

            return msbuildFiles;
        }

        private protected virtual DateTime GetLastWriteTimeUtcSafely(string file)
        {
            try
            {
                return File.GetLastWriteTimeUtc(file);
            }
            catch
            {
                return DateTime.UtcNow;
            }
        }

        static bool IsMsBuildFileExtension(string fileName)
        {
            var extension = Path.GetExtension(fileName.AsSpan());
            var hashCode = string.GetHashCode(extension, StringComparison.OrdinalIgnoreCase);
            for (var i = 0; i < s_msBuildFileExtensionHashes.Length; i++)
            {
                if (s_msBuildFileExtensionHashes[i] == hashCode && extension.Equals(s_msBuildFileExtensions[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
