// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Tools.ProjectExtensions
{
    internal static class ProjectExtensions
    {
        public static IEnumerable<string> GetRuntimeIdentifiers(this Project project)
        {
            return project
                .GetPropertyCommaSeparatedValues("RuntimeIdentifier")
                .Concat(project.GetPropertyCommaSeparatedValues("RuntimeIdentifiers"))
                .Select(value => value.ToLower())
                .Distinct();
        }

        public static IEnumerable<NuGetFramework> GetTargetFrameworks(this Project project)
        {
            var targetFrameworksStrings = project
                    .GetPropertyCommaSeparatedValues("TargetFramework")
                    .Union(project.GetPropertyCommaSeparatedValues("TargetFrameworks"))
                    .Select((value) => value.ToLower());

            var uniqueTargetFrameworkStrings = new HashSet<string>(targetFrameworksStrings);

            return uniqueTargetFrameworkStrings
                .Select((frameworkString) => NuGetFramework.Parse(frameworkString));
        }

        public static IEnumerable<string> GetConfigurations(this Project project)
        {
            return project.GetPropertyCommaSeparatedValues("Configurations");
        }

        public static IEnumerable<string> GetPropertyCommaSeparatedValues(this Project project, string propertyName)
        {
            return project.GetPropertyValue(propertyName)
                .Split(';')
                .Select((value) => value.Trim())
                .Where((value) => !string.IsNullOrEmpty(value));
        }

        public static string GetProjectFileFullPath(string projectFileOrDirectory)
        {
            if (File.Exists(projectFileOrDirectory))
            {
                return Path.GetFullPath(projectFileOrDirectory);
            }
            if (Directory.Exists(projectFileOrDirectory))
            {
                string[] files = [
                    ..Directory.GetFiles(projectFileOrDirectory, "*.csproj", SearchOption.TopDirectoryOnly),
                    ..Directory.GetFiles(projectFileOrDirectory, "*.vbproj", SearchOption.TopDirectoryOnly),
                    ..Directory.GetFiles(projectFileOrDirectory, "*.fsproj", SearchOption.TopDirectoryOnly),
                    ..Directory.GetFiles(projectFileOrDirectory, "*.proj", SearchOption.TopDirectoryOnly)];

                if (files.Length == 0)
                {
                    throw new GracefulException(CommonLocalizableStrings.CouldNotFindAnyProjectInDirectory, projectFileOrDirectory);
                }
                if (files.Length > 1)
                {
                    throw new GracefulException(CommonLocalizableStrings.MoreThanOneProjectInDirectory, projectFileOrDirectory);
                }
                return Path.GetFullPath(files.Single());
            }
            throw new GracefulException(CommonLocalizableStrings.CouldNotFindProjectOrDirectory, projectFileOrDirectory);
        }
    }
}
