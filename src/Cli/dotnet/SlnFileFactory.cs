// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
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

            SolutionModel slnFile;
            /*try
            {
                ISolutionSerializer serializer = SolutionSerializers.GetSerializerByMoniker(solutionPath) ?? throw new GracefulException(
                    CommonLocalizableStrings.CouldNotFindSolutionOrDirectory,
                    solutionPath);

                slnFile = serializer.OpenAsync(solutionPath, CancellationToken.None).Result;
            }
            catch (SolutionException e)
            {
                throw new GracefulException(
                    CommonLocalizableStrings.InvalidSolutionFormatString,
                    solutionPath,
                    e.Message);
            }*/
            ISolutionSerializer serializer = SolutionSerializers.GetSerializerByMoniker(solutionPath) ?? throw new GracefulException(
                    CommonLocalizableStrings.CouldNotFindSolutionOrDirectory,
                    solutionPath);

            slnFile = serializer.OpenAsync(solutionPath, CancellationToken.None).Result;
            return slnFile;
        }

        public static SolutionModel CreateFromFilteredSolutionFile(string filteredSolutionFile)
        {
            JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(filteredSolutionFile));
            JsonElement jsonElement = jsonDocument.RootElement;
            JsonElement filteredSolutionJsonElement = jsonElement.GetProperty("solution");
            string filteredSolutionPath = filteredSolutionJsonElement.GetProperty("path").GetString();
            string[] filteredSolutionProjectPaths = filteredSolutionJsonElement.GetProperty("projects")
                .EnumerateArray()
                .Select(project => project.GetString())
                .ToArray();

            if (string.IsNullOrEmpty(filteredSolutionPath) || !File.Exists(filteredSolutionPath))
            {
                throw new GracefulException(
                    CommonLocalizableStrings.InvalidSolutionFormatString,
                    filteredSolutionFile, "");
            }

            SolutionModel filteredSolution = new SolutionModel();
            SolutionModel originalSolution = CreateFromFileOrDirectory(filteredSolutionPath);

            foreach (string path in filteredSolutionProjectPaths)
            {
                SolutionProjectModel? project = originalSolution.FindProject(path);
                if (project is null)
                {
                    throw new GracefulException(
                        CommonLocalizableStrings.ProjectNotFoundInTheSolution,
                        path,
                        filteredSolutionPath);
                }
                filteredSolution.AddProject(project.FilePath, project.Type, project.Parent is null ? null : filteredSolution.AddFolder(project.Parent.Path));
            }

            return filteredSolution;
        }
    }
}
