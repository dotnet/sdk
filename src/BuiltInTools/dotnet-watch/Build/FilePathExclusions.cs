﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Graph;
using Microsoft.Build.Globbing;

namespace Microsoft.DotNet.Watch;

internal readonly struct FilePathExclusions(
    IEnumerable<(MSBuildGlob glob, string value, string projectDir)> exclusionGlobs,
    IReadOnlySet<string> outputDirectories)
{
    public static readonly FilePathExclusions Empty = new(exclusionGlobs: [], outputDirectories: new HashSet<string>());

    public static FilePathExclusions Create(ProjectGraph projectGraph)
    {
        var outputDirectories = new HashSet<string>(PathUtilities.OSSpecificPathComparer);
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
                    }
                }
            }
            else
            {
                // If default items are not enabled exclude just the output directories.

                TryAddOutputDir(projectNode.GetOutputDirectory());
                TryAddOutputDir(projectNode.GetIntermediateOutputDirectory());

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

    public void Report(IReporter reporter)
    {
        foreach (var globsPerDirectory in exclusionGlobs.GroupBy(keySelector: static g => g.projectDir, elementSelector: static g => g.value))
        {
            reporter.Verbose($"Exclusion glob: '{string.Join(";", globsPerDirectory)}' under project '{globsPerDirectory.Key}'");
        }

        foreach (var dir in outputDirectories)
        {
            reporter.Verbose($"Excluded directory: '{dir}'");
        }
    }

    internal bool IsExcluded(string fullPath, ChangeKind changeKind, IReporter reporter)
    {
        if (PathUtilities.ContainsPath(outputDirectories, fullPath))
        {
            reporter.Report(MessageDescriptor.IgnoringChangeInOutputDirectory, changeKind, fullPath);
            return true;
        }

        foreach (var (glob, globValue, projectDir) in exclusionGlobs)
        {
            if (glob.IsMatch(fullPath))
            {
                reporter.Report(MessageDescriptor.IgnoringChangeInExcludedFile, fullPath, changeKind, "DefaultItemExcludes", globValue, projectDir);
                return true;
            }
        }

        return false;
    }
}
