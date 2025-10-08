// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Text.Json;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;

namespace Microsoft.DotNet.Cli;

public static class SlnFileFactory
{
    public static readonly string[] DefaultPlatforms = new[] { "Any CPU", "x64", "x86" };
    public static readonly string[] DefaultBuildTypes = new[] { "Debug", "Release" };

    public static string GetSolutionFileFullPath(string slnFileOrDirectory, bool includeSolutionFilterFiles = false, bool includeSolutionXmlFiles = true)
    {
        // Throw error if slnFileOrDirectory is an invalid path
        if (string.IsNullOrWhiteSpace(slnFileOrDirectory) || slnFileOrDirectory.IndexOfAny(Path.GetInvalidPathChars()) != -1)
        {
            throw new GracefulException(CliStrings.CouldNotFindSolutionOrDirectory);
        }
        if (File.Exists(slnFileOrDirectory))
        {
            return Path.GetFullPath(slnFileOrDirectory);
        }
        if (Directory.Exists(slnFileOrDirectory))
        {
            string[] files = ListSolutionFilesInDirectory(slnFileOrDirectory, includeSolutionFilterFiles, includeSolutionXmlFiles);
            if (files.Length == 0)
            {
                throw new GracefulException(
                    CliStrings.CouldNotFindSolutionIn,
                    slnFileOrDirectory);
            }
            if (files.Length > 1)
            {
                throw new GracefulException(
                    CliStrings.MoreThanOneSolutionInDirectory,
                    slnFileOrDirectory);
            }
            return Path.GetFullPath(files.Single());
        }
        throw new GracefulException(
            CliStrings.CouldNotFindSolutionOrDirectory,
            slnFileOrDirectory);
    }


    public static string[] ListSolutionFilesInDirectory(string directory, bool includeSolutionFilterFiles = false, bool includeSolutionXmlFiles = true)
    {
        return
        [
            .. Directory.GetFiles(directory, "*.sln", SearchOption.TopDirectoryOnly),
            .. includeSolutionXmlFiles ? Directory.GetFiles(directory, "*.slnx", SearchOption.TopDirectoryOnly) : [],
            .. includeSolutionFilterFiles ? Directory.GetFiles(directory, "*.slnf", SearchOption.TopDirectoryOnly) : []
        ];
    }

    public static SolutionModel CreateFromFileOrDirectory(string fileOrDirectory, bool includeSolutionFilterFiles = false, bool includeSolutionXmlFiles = true)
    {
        string solutionPath = GetSolutionFileFullPath(fileOrDirectory, includeSolutionFilterFiles, includeSolutionXmlFiles);

        if (solutionPath.HasExtension(".slnf"))
        {
            return CreateFromFilteredSolutionFile(solutionPath);
        }
        try
        {
            ISolutionSerializer serializer = SolutionSerializers.GetSerializerByMoniker(solutionPath)!;
            return serializer.OpenAsync(solutionPath, CancellationToken.None).Result;
        }
        catch (Exception ex)
        {
            throw new GracefulException(
                CliStrings.InvalidSolutionFormatString,
                solutionPath, ex.Message);
        }
    }

    public static SolutionModel CreateFromFilteredSolutionFile(string filteredSolutionPath)
    {
        string originalSolutionPath;
        string originalSolutionPathAbsolute;
        IEnumerable<string> filteredSolutionProjectPaths;
        try
        {
            JsonElement root = JsonDocument.Parse(File.ReadAllText(filteredSolutionPath)).RootElement;
            originalSolutionPath = Uri.UnescapeDataString(root.GetProperty("solution").GetProperty("path").GetString());
            // Normalize path separators to OS-specific for cross-platform compatibility
            originalSolutionPath = originalSolutionPath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            filteredSolutionProjectPaths = [.. root.GetProperty("solution").GetProperty("projects").EnumerateArray().Select(p => p.GetString())];
            originalSolutionPathAbsolute = Path.GetFullPath(originalSolutionPath, Path.GetDirectoryName(filteredSolutionPath));
        }
        catch (Exception ex)
        {
            throw new GracefulException(
                CliStrings.InvalidSolutionFormatString,
                filteredSolutionPath, ex.Message);
        }

        SolutionModel filteredSolution = new();
        SolutionModel originalSolution = CreateFromFileOrDirectory(originalSolutionPathAbsolute);

        // Store the original solution path in the description field of the filtered solution
        filteredSolution.Description = originalSolutionPathAbsolute;

        foreach (var platform in originalSolution.Platforms)
        {
            filteredSolution.AddPlatform(platform);
        }
        foreach (var buildType in originalSolution.BuildTypes)
        {
            filteredSolution.AddBuildType(buildType);
        }

        IEnumerable<SolutionProjectModel> projects = filteredSolutionProjectPaths
            .Select(path => path.Replace('\\', Path.DirectorySeparatorChar))
            .Select(path => Uri.UnescapeDataString(path))
            .Select(path => originalSolution.FindProject(path) ?? throw new GracefulException(
                    CliStrings.ProjectNotFoundInTheSolution,
                    path,
                    originalSolutionPath));

        foreach (var project in projects)
        {
            _ = filteredSolution.AddProject(
                project.FilePath,
                project.Type,
                project.Parent is null ? null : filteredSolution.AddFolder(project.Parent.Path));
        }

        return filteredSolution;
    }
}
