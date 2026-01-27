// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Graph;
using Microsoft.Build.Globbing;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal readonly struct FilePathExclusions(
    IEnumerable<(MSBuildGlob glob, string value, string projectDir)> exclusionGlobs,
    IReadOnlySet<string> outputDirectories,
    IReadOnlySet<string> excludedDirectories)
{
    public static readonly FilePathExclusions Empty = new(exclusionGlobs: [], outputDirectories: new HashSet<string>(), excludedDirectories: new HashSet<string>());

    public IReadOnlySet<string> ExcludedDirectories => excludedDirectories;

    public static FilePathExclusions Create(ProjectGraph projectGraph)
    {
        var outputDirectories = new HashSet<string>(PathUtilities.OSSpecificPathComparer);
        var excludedDirectories = new HashSet<string>(PathUtilities.OSSpecificPathComparer);
        var globs = new Dictionary<(string fixedDirectoryPart, string wildcardDirectoryPart, string filenamePart), (MSBuildGlob glob, string value, string projectDir)>();

        foreach (var projectNode in projectGraph.ProjectNodes)
        {
            if (projectNode.AreDefaultItemsEnabled())
            {
                var projectDir = projectNode.ProjectInstance.Directory;

                foreach (var globValue in projectNode.GetDefaultItemExcludes())
                {
                    var glob = MSBuildGlob.Parse(projectDir, globValue);
                    if (glob.IsLegal)
                    {
                        // The glob creates regex based on the three parts of the glob.
                        // Avoid adding duplicate globs that match the same files.
                        globs.TryAdd((glob.FixedDirectoryPart, glob.WildcardDirectoryPart, glob.FilenamePart), (glob, globValue, projectDir));

                        // If the glob excludes an entire subdirectory (e.g., ends with /** or /**/*.*)
                        // we can avoid watching that subdirectory
                        if (IsDirectoryExclusionPattern(glob, globValue))
                        {
                            var excludedDir = GetExcludedDirectory(glob);
                            if (excludedDir != null)
                            {
                                excludedDirectories.Add(excludedDir);
                            }
                        }
                    }
                }
            }
            else
            {
                // If default items are not enabled exclude just the output directories.

                TryAddOutputDir(projectNode.GetOutputDirectory());
                TryAddOutputDir(projectNode.ProjectInstance.GetIntermediateOutputDirectory());

                void TryAddOutputDir(string? dir)
                {
                    try
                    {
                        if (dir != null)
                        {
                            // msbuild properties may use '\' as a directory separator even on Unix.
                            // GetFullPath does not normalize '\' to '/' on Unix.
                            if (Path.DirectorySeparatorChar == '/')
                            {
                                dir = dir.Replace('\\', '/');
                            }

                            outputDirectories.Add(Path.TrimEndingDirectorySeparator(Path.GetFullPath(dir)));
                        }
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }
        }

        return new FilePathExclusions(globs.Values, outputDirectories, excludedDirectories);
    }

    /// <summary>
    /// Determines if a glob pattern excludes an entire subdirectory recursively.
    /// Patterns like "dir/**" or "dir/**/*.*" exclude all contents of "dir".
    /// </summary>
    private static bool IsDirectoryExclusionPattern(MSBuildGlob glob, string globValue)
    {
        // Check if the pattern ends with /** or /**/* or /**/*.*
        // These patterns match all files recursively in a directory
        return globValue.EndsWith("/**", StringComparison.Ordinal) ||
               globValue.EndsWith("/**/*", StringComparison.Ordinal) ||
               globValue.EndsWith("/**/*.*", StringComparison.Ordinal);
    }

    /// <summary>
    /// Extracts the directory path that should be excluded from watching.
    /// </summary>
    private static string? GetExcludedDirectory(MSBuildGlob glob)
    {
        try
        {
            // The FixedDirectoryPart contains the path up to the first wildcard
            // For patterns like "C:/project/bin/**", FixedDirectoryPart is "C:/project/bin/"
            var fixedPart = glob.FixedDirectoryPart;
            
            if (!string.IsNullOrEmpty(fixedPart))
            {
                // Remove trailing directory separator and get the full path
                return Path.TrimEndingDirectorySeparator(Path.GetFullPath(fixedPart));
            }
        }
        catch (ArgumentException)
        {
            // Path contains invalid characters or is invalid
        }
        catch (NotSupportedException)
        {
            // Path contains a colon in a position other than the drive letter
        }
        catch (PathTooLongException)
        {
            // Path is too long
        }

        return null;
    }

    public void Report(ILogger log)
    {
        foreach (var globsPerDirectory in exclusionGlobs.GroupBy(keySelector: static g => g.projectDir, elementSelector: static g => g.value))
        {
            log.LogDebug("Exclusion glob: '{Globs}' under project '{Directory}'", string.Join(";", globsPerDirectory), globsPerDirectory.Key);
        }

        foreach (var dir in outputDirectories)
        {
            log.LogDebug("Excluded directory: '{Directory}'", dir);
        }

        foreach (var dir in excludedDirectories)
        {
            log.LogDebug("Excluded directory from watching: '{Directory}'", dir);
        }
    }

    internal bool IsExcluded(string fullPath, ChangeKind changeKind, ILogger logger)
    {
        if (PathUtilities.ContainsPath(outputDirectories, fullPath))
        {
            logger.Log(MessageDescriptor.IgnoringChangeInOutputDirectory, changeKind, fullPath);
            return true;
        }

        foreach (var (glob, globValue, projectDir) in exclusionGlobs)
        {
            if (glob.IsMatch(fullPath))
            {
                logger.Log(MessageDescriptor.IgnoringChangeInExcludedFile, fullPath, changeKind, "DefaultItemExcludes", globValue, projectDir);
                return true;
            }
        }

        return false;
    }
}
