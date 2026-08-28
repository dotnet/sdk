// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks;

/// <summary>
/// Validates a positional source-to-destination layout mapping and identifies outputs
/// that are no longer produced by that mapping.
/// </summary>
/// <remarks>
/// <see cref="ExistingOutputs"/> must contain only files owned by the caller. This task
/// treats every listed file outside the expected destination set as stale. All path-bearing
/// properties must contain fully qualified paths, and the source and destination sets must
/// be disjoint.
/// </remarks>
public sealed class PrepareIncrementalLayout : Task
{
    /// <summary>
    /// Gets or sets the fully qualified source files to lay out. Each item maps by index
    /// to an item in <see cref="DestinationFiles"/>.
    /// </summary>
    [Required]
    public ITaskItem[] SourceFiles { get; set; } = [];

    /// <summary>
    /// Gets or sets the fully qualified destination files. Each item maps by index to an
    /// item in <see cref="SourceFiles"/>.
    /// </summary>
    [Required]
    public ITaskItem[] DestinationFiles { get; set; } = [];

    /// <summary>
    /// Gets or sets the fully qualified existing files owned by the caller's layout. Files
    /// not present in <see cref="ExpectedOutputs"/> are reported through
    /// <see cref="StaleOutputs"/>.
    /// </summary>
    public ITaskItem[] ExistingOutputs { get; set; } = [];

    /// <summary>
    /// Gets the normalized destination files expected from the current mapping.
    /// </summary>
    [Output]
    public ITaskItem[] ExpectedOutputs { get; private set; } = [];

    /// <summary>
    /// Gets the normalized existing outputs that are no longer destinations of the
    /// current mapping.
    /// </summary>
    [Output]
    public ITaskItem[] StaleOutputs { get; private set; } = [];

    public override bool Execute()
    {
        try
        {
            if (SourceFiles.Length != DestinationFiles.Length)
            {
                Log.LogError(
                    $"The incremental layout has {SourceFiles.Length} source files but {DestinationFiles.Length} destination files.");
                return false;
            }

            StringComparer pathComparer = IncrementalLayoutState.PathComparer;
            var sourcePaths = new HashSet<string>(pathComparer);
            var destinationPaths = new HashSet<string>(pathComparer);
            var expectedOutputs = new List<ITaskItem>(SourceFiles.Length);

            for (int index = 0; index < SourceFiles.Length; index++)
            {
                bool sourcePathIsValid = IncrementalLayoutState.TryGetFullPath(
                    Log,
                    SourceFiles[index].ItemSpec,
                    nameof(SourceFiles),
                    out string sourcePath);
                bool destinationPathIsValid = IncrementalLayoutState.TryGetFullPath(
                    Log,
                    DestinationFiles[index].ItemSpec,
                    nameof(DestinationFiles),
                    out string destinationPath);

                if (!sourcePathIsValid || !destinationPathIsValid)
                {
                    continue;
                }

                sourcePaths.Add(sourcePath);

                if (!File.Exists(sourcePath))
                {
                    Log.LogError($"Incremental layout input '{sourcePath}' does not exist.");
                    continue;
                }

                if (!destinationPaths.Add(destinationPath))
                {
                    Log.LogError($"Incremental layout destination '{destinationPath}' is produced by more than one input.");
                    continue;
                }

                expectedOutputs.Add(new TaskItem(destinationPath));
            }

            foreach (string sourcePath in sourcePaths)
            {
                if (destinationPaths.Contains(sourcePath))
                {
                    Log.LogError($"Incremental layout input '{sourcePath}' must not also be a destination.");
                }
            }

            ExpectedOutputs = expectedOutputs.ToArray();
            var staleOutputPaths = new HashSet<string>(pathComparer);
            var staleOutputs = new List<ITaskItem>();
            foreach (ITaskItem existingOutput in ExistingOutputs)
            {
                if (IncrementalLayoutState.TryGetFullPath(
                    Log,
                    existingOutput.ItemSpec,
                    nameof(ExistingOutputs),
                    out string existingOutputPath)
                    && !destinationPaths.Contains(existingOutputPath)
                    && staleOutputPaths.Add(existingOutputPath))
                {
                    staleOutputs.Add(new TaskItem(existingOutputPath));
                }
            }

            StaleOutputs = staleOutputs.ToArray();

            return !Log.HasLoggedErrors;
        }
        catch (Exception exception)
        {
            Log.LogErrorFromException(exception, showStackTrace: true);
            return false;
        }
    }
}

/// <summary>
/// Verifies that every expected layout output exists, then writes the completion stamp.
/// </summary>
/// <remarks>
/// The completion stamp is written atomically and only after all outputs are present, so
/// an interrupted or incomplete layout cannot appear complete to subsequent builds. All
/// path-bearing properties must contain fully qualified paths.
/// </remarks>
public sealed class CompleteIncrementalLayout : Task
{
    internal const string CompletionMarker = "complete";

    /// <summary>
    /// Gets or sets the fully qualified files that must exist before the layout can be
    /// marked complete.
    /// </summary>
    [Required]
    public ITaskItem[] ExpectedOutputs { get; set; } = [];

    /// <summary>
    /// Gets or sets the fully qualified completion stamp to write after every expected
    /// output is present.
    /// </summary>
    [Required]
    public string CompletionStampFile { get; set; } = string.Empty;

    public override bool Execute()
    {
        try
        {
            bool completionStampPathIsValid = IncrementalLayoutState.TryGetFullPath(
                Log,
                CompletionStampFile,
                nameof(CompletionStampFile),
                out string completionStampPath);

            if (completionStampPathIsValid)
            {
                File.Delete(completionStampPath);
            }

            foreach (ITaskItem expectedOutput in ExpectedOutputs)
            {
                if (IncrementalLayoutState.TryGetFullPath(
                    Log,
                    expectedOutput.ItemSpec,
                    nameof(ExpectedOutputs),
                    out string outputPath)
                    && !File.Exists(outputPath))
                {
                    Log.LogError($"Incremental layout output '{outputPath}' was not produced.");
                }
            }

            if (!completionStampPathIsValid || Log.HasLoggedErrors)
            {
                return false;
            }

            IncrementalLayoutState.WriteFile(
                completionStampPath,
                CompletionMarker + Environment.NewLine);

            return true;
        }
        catch (Exception exception)
        {
            Log.LogErrorFromException(exception, showStackTrace: true);
            return false;
        }
    }
}

internal static class IncrementalLayoutState
{
    public static StringComparer PathComparer { get; } =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static bool TryGetFullPath(
        TaskLoggingHelper log,
        string path,
        string parameterName,
        out string fullPath)
    {
        if (!Path.IsPathFullyQualified(path))
        {
            log.LogError($"Incremental layout path '{path}' from '{parameterName}' must be fully qualified.");
            fullPath = string.Empty;
            return false;
        }

        fullPath = Path.GetFullPath(path);
        return true;
    }

    public static void WriteFile(string path, string contents)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        string temporaryPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }
}
