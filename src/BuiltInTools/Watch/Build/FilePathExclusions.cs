// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Graph;
using Microsoft.Build.Globbing;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch;

internal readonly struct ExclusionGlob(string projectDir, MSBuildGlob glob, string value, MSBuildGlob? encompassingRootGlob)
{
    public string ProjectDir { get; } = projectDir;
    public MSBuildGlob Glob { get; } = glob;
    public MSBuildGlob? EncompassingRootGlob { get; } = encompassingRootGlob;
    public string Value { get; } = value;
}

internal readonly struct FilePathExclusions(IEnumerable<ExclusionGlob> exclusionGlobs, IReadOnlySet<string> outputDirectories)
{
    public static readonly FilePathExclusions Empty = new(exclusionGlobs: [], outputDirectories: new HashSet<string>());

    public static FilePathExclusions Create(ProjectGraph projectGraph)
    {
        var outputDirectories = new HashSet<string>(PathUtilities.OSSpecificPathComparer);
        var globs = new Dictionary<(string fixedDirectoryPart, string wildcardDirectoryPart, string filenamePart), ExclusionGlob>();

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
                        var encompassingRootGlob = TryGetEncompassingRootDirectoryPattern(globValue, out var encompassingRootPattern)
                            ? MSBuildGlob.Parse(projectDir, globValue)
                            : null;

                        // The glob creates regex based on the three parts of the glob.
                        // Avoid adding duplicate globs that match the same files.
                        globs.TryAdd(
                            (glob.FixedDirectoryPart, glob.WildcardDirectoryPart, glob.FilenamePart),
                            new ExclusionGlob(projectDir, glob, globValue, encompassingRootGlob));
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

        return new FilePathExclusions(globs.Values, outputDirectories);
    }

    public void Report(ILogger log)
    {
        foreach (var globsPerDirectory in exclusionGlobs.GroupBy(keySelector: static g => g.ProjectDir, elementSelector: static g => g.Value))
        {
            log.LogDebug("Exclusion glob: '{Globs}' under project '{Directory}'", string.Join(";", globsPerDirectory), globsPerDirectory.Key);
        }

        foreach (var dir in outputDirectories)
        {
            log.LogDebug("Excluded directory: '{Directory}'", dir);
        }
    }

    public bool IsExcluded(string fullPath, ChangeKind changeKind, ILogger logger)
    {
        if (PathUtilities.ContainsPath(outputDirectories, fullPath))
        {
            logger.Log(MessageDescriptor.IgnoringChangeInOutputDirectory, changeKind, fullPath);
            return true;
        }

        foreach (var exclusionGlob in exclusionGlobs)
        {
            if (exclusionGlob.Glob.IsMatch(fullPath))
            {
                logger.Log(MessageDescriptor.IgnoringChangeInExcludedFile, fullPath, changeKind, "DefaultItemExcludes", exclusionGlob.Value, exclusionGlob.ProjectDir);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True if the entire subdirectory <paramref name="directoryPath"/> is excluded.
    /// </summary>
    public bool IsExcludedSubdirectory(string directoryPath)
        => exclusionGlobs.Any(exclusionGlob => exclusionGlob.EncompassingRootGlob?.IsMatch(directoryPath) == true) ||
           PathUtilities.ContainsPath(outputDirectories, directoryPath);

    /// <summary>
    /// If the <paramref name="pattern"/> matches all possible files and directories under a root directory,
    /// returns true and the pattern that matches all such root directories. Otherwise, returns false.
    /// </summary>
    internal static bool TryGetEncompassingRootDirectoryPattern(ReadOnlySpan<char> pattern, out ReadOnlySpan<char> rootPattern)
    {
        var hasDoubleStar = false;
        while (true)
        {
            var separator = pattern.LastIndexOfAny('/', '\\');
            var part = pattern[(separator + 1)..];

            if (part is "")
            {
                continue;
            }

            if (part is not ("*" or "**"))
            {
                break;
            }

            hasDoubleStar |= part is "**";
            pattern = pattern[..separator];
        }

        rootPattern = pattern;
        return hasDoubleStar;
    }
}
