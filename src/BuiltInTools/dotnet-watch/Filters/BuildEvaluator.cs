// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Diagnostics;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal class BuildEvaluator(DotNetWatchContext context, FileSetFactory factory)
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
        private (ProjectInfo project, FileSet fileSet)? _evaluationResult;

        public bool RequiresRevaluation { get; set; }

        private bool _canUseNoRestore;
        private string[]? _noRestoreArguments;

        public void UpdateProcessArguments(ProcessSpec processSpec, int iteration)
        {
            if (context.EnvironmentOptions.SuppressMSBuildIncrementalism)
            {
                return;
            }

            if (iteration == 0)
            {
                var arguments = processSpec.Arguments ?? [];
                _canUseNoRestore = CanUseNoRestore(arguments);
                if (_canUseNoRestore)
                {
                    // Create run --no-restore <other args>
                    _noRestoreArguments = arguments.Take(1).Append("--no-restore").Concat(arguments.Skip(1)).ToArray();
                    context.Reporter.Verbose($"No restore arguments: {string.Join(" ", _noRestoreArguments)}");
                }
            }
            else if (_canUseNoRestore)
            {
                if (RequiresRevaluation)
                {
                    context.Reporter.Verbose("Cannot use --no-restore since msbuild project files have changed.");
                }
                else
                {
                    context.Reporter.Verbose("Modifying command to use --no-restore");
                    processSpec.Arguments = _noRestoreArguments;
                }
            }
        }

        private bool CanUseNoRestore(IEnumerable<string> arguments)
        {
            // For some well-known dotnet commands, we can pass in the --no-restore switch to avoid unnecessary restores between iterations.
            // For now we'll support the "run" and "test" commands.
            if (arguments.Any(a => string.Equals(a, "--no-restore", StringComparison.Ordinal)))
            {
                // Did the user already configure a --no-restore?
                return false;
            }

            var dotnetCommand = arguments.FirstOrDefault();
            if (string.Equals(dotnetCommand, "run", StringComparison.Ordinal) || string.Equals(dotnetCommand, "test", StringComparison.Ordinal))
            {
                context.Reporter.Verbose("Watch command can be configured to use --no-restore.");
                return true;
            }
            else
            {
                context.Reporter.Verbose($"Watch command will not use --no-restore. Unsupport dotnet-command '{dotnetCommand}'.");
                return false;
            }
        }

        public async ValueTask<(ProjectInfo, FileSet)?> EvaluateAsync(FileItem? changedFile, CancellationToken cancellationToken)
        {
            if (context.EnvironmentOptions.SuppressMSBuildIncrementalism)
            {
                RequiresRevaluation = true;
                return _evaluationResult = await factory.CreateAsync(cancellationToken);
            }

            if (!_evaluationResult.HasValue || RequiresMSBuildRevaluation(changedFile))
            {
                RequiresRevaluation = true;
            }

            if (RequiresRevaluation)
            {
                context.Reporter.Verbose("Evaluating dotnet-watch file set.");

                var result = await factory.CreateAsync(cancellationToken);
                _msbuildFileTimestamps = GetMSBuildFileTimeStamps(result.files);
                return _evaluationResult = result;
            }

            Debug.Assert(_evaluationResult != null);
            return _evaluationResult;
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

        private List<(string fileName, DateTime lastModifiedUtc)> GetMSBuildFileTimeStamps(FileSet files)
        {
            var msbuildFiles = new List<(string fileName, DateTime lastModifiedUtc)>();
            foreach (var file in files)
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
