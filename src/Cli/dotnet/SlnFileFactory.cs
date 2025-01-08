// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;

namespace Microsoft.DotNet.Tools.Common
{
    public static class SlnFileFactory
    {
        public static string[] ListSolutionFilesInDirectory(string directory, bool includeSolutionFilterFiles = false)
        {
            return [
                ..Directory.GetFiles(directory, "*.sln", SearchOption.TopDirectoryOnly),
                ..Directory.GetFiles(directory, "*.slnx", SearchOption.TopDirectoryOnly),
                ..(includeSolutionFilterFiles ? Directory.GetFiles(directory, "*.slnf", SearchOption.TopDirectoryOnly) : Array.Empty<string>())
            ];
        }

        public static SolutionModel CreateFromFileOrDirectory(string fileOrDirectory)
        {
            if (File.Exists(fileOrDirectory))
            {
                return FromFile(fileOrDirectory);
            }
            else
            {
                return FromDirectory(fileOrDirectory);
            }
        }

        private static SolutionModel FromFile(string solutionPath)
        {
            SolutionModel slnFile = null;
            try
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
            }
            return slnFile;
        }

        private static SolutionModel FromDirectory(string solutionDirectory)
        {
            DirectoryInfo dir;
            try
            {
                dir = new DirectoryInfo(solutionDirectory);
                if (!dir.Exists)
                {
                    throw new GracefulException(
                        CommonLocalizableStrings.CouldNotFindSolutionOrDirectory,
                        solutionDirectory);
                }
            }
            catch (ArgumentException)
            {
                throw new GracefulException(
                    CommonLocalizableStrings.CouldNotFindSolutionOrDirectory,
                    solutionDirectory);
            }

            FileInfo[] files = [..dir.GetFiles("*.sln"), ..dir.GetFiles("*.slnx")];
            if (files.Length == 0)
            {
                throw new GracefulException(
                    CommonLocalizableStrings.CouldNotFindSolutionIn,
                    solutionDirectory);
            }

            if (files.Length > 1)
            {
                throw new GracefulException(
                    CommonLocalizableStrings.MoreThanOneSolutionInDirectory,
                    solutionDirectory);
            }

            FileInfo solutionFile = files.Single();
            if (!solutionFile.Exists)
            {
                throw new GracefulException(
                    CommonLocalizableStrings.CouldNotFindSolutionIn,
                    solutionDirectory);
            }

            return FromFile(solutionFile.FullName);
        }
    }
}
