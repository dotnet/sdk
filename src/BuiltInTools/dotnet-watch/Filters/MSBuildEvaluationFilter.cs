// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Diagnostics;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal class MSBuildEvaluationFilter(DotNetWatchContext context, IFileSetFactory factory)
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

        public async ValueTask ProcessAsync(WatchState state, CancellationToken cancellationToken)
        {
            if (context.EnvironmentOptions.SuppressMSBuildIncrementalism)
            {
                state.RequiresMSBuildRevaluation = true;
                state.FileSet = await factory.CreateAsync(waitOnError: true, cancellationToken);
                return;
            }

            if (state.Iteration == 0 || RequiresMSBuildRevaluation(state))
            {
                state.RequiresMSBuildRevaluation = true;
            }

            if (state.RequiresMSBuildRevaluation)
            {
                context.Reporter.Verbose("Evaluating dotnet-watch file set.");

                state.FileSet = await factory.CreateAsync(waitOnError: true, cancellationToken);
                _msbuildFileTimestamps = GetMSBuildFileTimeStamps(state);
            }
        }

        private bool RequiresMSBuildRevaluation(WatchState state)
        {
            Debug.Assert(state.Iteration > 0);
            Debug.Assert(_msbuildFileTimestamps != null);

            var changedFile = state.ChangedFile;
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

        private List<(string fileName, DateTime lastModifiedUtc)> GetMSBuildFileTimeStamps(WatchState state)
        {
            Debug.Assert(state.FileSet != null);

            var msbuildFiles = new List<(string fileName, DateTime lastModifiedUtc)>();
            foreach (var file in state.FileSet)
            {
                if (!string.IsNullOrEmpty(file.FilePath) && IsMsBuildFileExtension(file.FilePath))
                {
                    msbuildFiles.Add((file.FilePath, GetLastWriteTimeUtcSafely(file.FilePath)));
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
#pragma warning disable RS1024 // Analyzer bug - https://github.com/dotnet/roslyn-analyzers/issues/4956
            var hashCode = string.GetHashCode(extension, StringComparison.OrdinalIgnoreCase);
#pragma warning restore RS1024
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
