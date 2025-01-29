// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Nodes;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;

namespace Microsoft.DotNet.Tools.Common
{
    public static class SlnFileFactory
    {
        public static string GetSolutionFileFullPath(string slnFileOrDirectory, bool includeSolutionFilterFiles = false, bool includeSolutionXmlFiles = true)
        {
            // Throw error if slnFileOrDirectory is an invalid path
            if (string.IsNullOrWhiteSpace(slnFileOrDirectory) || slnFileOrDirectory.IndexOfAny(Path.GetInvalidPathChars()) != -1)
            {
                throw new GracefulException(CommonLocalizableStrings.CouldNotFindSolutionOrDirectory);
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
                        CommonLocalizableStrings.CouldNotFindSolutionIn,
                        slnFileOrDirectory);
                }
                if (files.Length > 1)
                {
                    throw new GracefulException(
                        CommonLocalizableStrings.MoreThanOneSolutionInDirectory,
                        slnFileOrDirectory);
                }
                return Path.GetFullPath(files.Single());
            }
            throw new GracefulException(
                CommonLocalizableStrings.CouldNotFindSolutionOrDirectory,
                slnFileOrDirectory);
        }


        public static string[] ListSolutionFilesInDirectory(string directory, bool includeSolutionFilterFiles = false, bool includeSolutionXmlFiles = true)
        {
            return [
                ..Directory.GetFiles(directory, "*.sln", SearchOption.TopDirectoryOnly),
                ..(includeSolutionXmlFiles ? Directory.GetFiles(directory, "*.slnx", SearchOption.TopDirectoryOnly) : []),
                ..(includeSolutionFilterFiles ? Directory.GetFiles(directory, "*.slnf", SearchOption.TopDirectoryOnly) : [])
            ];
        }

        public static SolutionModel CreateFromFileOrDirectory(string fileOrDirectory, bool includeSolutionFilterFiles = false, bool includeSolutionXmlFiles = true)
        {
            string solutionPath = GetSolutionFileFullPath(fileOrDirectory, includeSolutionFilterFiles, includeSolutionXmlFiles);

            if (solutionPath.HasExtension(".slnf"))
            {
                return CreateFromFilteredSolutionFile(solutionPath);
            }
            ISolutionSerializer serializer = SolutionSerializers.GetSerializerByMoniker(solutionPath) ?? throw new GracefulException(
                    CommonLocalizableStrings.CouldNotFindSolutionOrDirectory,
                    solutionPath);

            return serializer.OpenAsync(solutionPath, CancellationToken.None).Result;
        }

        public static SolutionModel CreateFromFilteredSolutionFile(string filteredSolutionPath)
        {
            string originalSolutionPath;
            string originalSolutionPathAbsolute;
            IEnumerable<string> filteredSolutionProjectPaths;
            try
            {
                JsonNode? jsonNode = JsonNode.Parse(File.ReadAllText(filteredSolutionPath));
                originalSolutionPath = jsonNode?["solution"]?["path"]?.ToString();
                filteredSolutionProjectPaths = jsonNode["solution"]?["projects"]?.AsArray()?.GetValues<string>().ToArray() ?? [];
                originalSolutionPathAbsolute = Path.GetFullPath(originalSolutionPath, Path.GetDirectoryName(filteredSolutionPath));
            }
            catch (Exception ex) {
                throw new GracefulException(
                    CommonLocalizableStrings.InvalidSolutionFormatString,
                    filteredSolutionPath, ex.Message);
            }

            SolutionModel filteredSolution = new();
            SolutionModel originalSolution = CreateFromFileOrDirectory(originalSolutionPathAbsolute);

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
                .Select(path => originalSolution.FindProject(path) ?? throw new GracefulException(
                        CommonLocalizableStrings.ProjectNotFoundInTheSolution,
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
}
