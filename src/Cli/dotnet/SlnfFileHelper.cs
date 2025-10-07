// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli;

/// <summary>
/// Utilities for working with solution filter (.slnf) files
/// </summary>
public static class SlnfFileHelper
{
    private class SlnfSolution
    {
        [JsonPropertyName("path")]
        public string Path { get; set; }

        [JsonPropertyName("projects")]
        public List<string> Projects { get; set; } = new();
    }

    private class SlnfRoot
    {
        [JsonPropertyName("solution")]
        public SlnfSolution Solution { get; set; } = new();
    }

    /// <summary>
    /// Creates a new solution filter file
    /// </summary>
    /// <param name="slnfPath">Path to the solution filter file to create</param>
    /// <param name="parentSolutionPath">Path to the parent solution file</param>
    /// <param name="projects">List of project paths to include (relative to the parent solution)</param>
    public static void CreateSolutionFilter(string slnfPath, string parentSolutionPath, IEnumerable<string> projects = null)
    {
        var slnfDirectory = Path.GetDirectoryName(Path.GetFullPath(slnfPath));
        var parentSolutionFullPath = Path.GetFullPath(parentSolutionPath, slnfDirectory);
        var relativeSolutionPath = Path.GetRelativePath(slnfDirectory, parentSolutionFullPath);

        // Normalize path separators to backslashes (as per slnf format)
        relativeSolutionPath = relativeSolutionPath.Replace(Path.DirectorySeparatorChar, '\\');

        var root = new SlnfRoot
        {
            Solution = new SlnfSolution
            {
                Path = relativeSolutionPath,
                Projects = projects?.Select(p => p.Replace(Path.DirectorySeparatorChar, '\\')).ToList() ?? new List<string>()
            }
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };

        var json = JsonSerializer.Serialize(root, options);
        File.WriteAllText(slnfPath, json);
    }

    /// <summary>
    /// Saves a solution filter file with the given projects
    /// </summary>
    /// <param name="slnfPath">Path to the solution filter file</param>
    /// <param name="parentSolutionPath">Path to the parent solution (stored in the slnf file)</param>
    /// <param name="projects">List of project paths (relative to the parent solution)</param>
    public static void SaveSolutionFilter(string slnfPath, string parentSolutionPath, IEnumerable<string> projects)
    {
        var slnfDirectory = Path.GetDirectoryName(Path.GetFullPath(slnfPath));
        
        // Normalize the parent solution path to be relative to the slnf file
        var relativeSolutionPath = parentSolutionPath;
        if (Path.IsPathRooted(parentSolutionPath))
        {
            relativeSolutionPath = Path.GetRelativePath(slnfDirectory, parentSolutionPath);
        }
        
        // Normalize path separators to backslashes (as per slnf format)
        relativeSolutionPath = relativeSolutionPath.Replace(Path.DirectorySeparatorChar, '\\');

        var root = new SlnfRoot
        {
            Solution = new SlnfSolution
            {
                Path = relativeSolutionPath,
                Projects = projects.Select(p => p.Replace(Path.DirectorySeparatorChar, '\\')).ToList()
            }
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };

        var json = JsonSerializer.Serialize(root, options);
        File.WriteAllText(slnfPath, json);
    }
}
