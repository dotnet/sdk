﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;

namespace Microsoft.DotNet.Tools.Common
{
    public static class SlnFileFactory
    {
        public static string[] ListSolutionFilesInDirectory(string directory, bool includeSolutionFilterFiles = false, bool includeSolutionXmlFiles = true)
        {
            return [
                ..Directory.GetFiles(directory, "*.sln", SearchOption.TopDirectoryOnly),
                ..(includeSolutionXmlFiles ? Directory.GetFiles(directory, "*.slnx", SearchOption.TopDirectoryOnly) : []),
                ..(includeSolutionFilterFiles ? Directory.GetFiles(directory, "*.slnf", SearchOption.TopDirectoryOnly) : [])
            ];
        }

        public static SolutionModel CreateFromFileOrDirectory(string fileOrDirectory, bool includeSolutionXmlFiles = true)
        {
            if (File.Exists(fileOrDirectory))
            {
                return FromFile(fileOrDirectory);
            }
            else
            {
                return FromDirectory(fileOrDirectory, includeSolutionXmlFiles);
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

        private static SolutionModel FromDirectory(string solutionDirectory, bool includeSolutionXmlFiles)
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

            FileInfo[] files = [
                ..dir.GetFiles("*.sln"),
                ..(includeSolutionXmlFiles ? dir.GetFiles(".slnx") : [])
             ];

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
