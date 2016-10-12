// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using System;
using System.IO;

namespace Microsoft.NET.TestFramework.Commands
{
    public abstract class TestCommand
    {
        public MSBuildTest MSBuild { get; }

        public string ProjectRootPath { get; }

        public string ProjectFile { get; }

        public string FullPathProjectFile => Path.Combine(ProjectRootPath, ProjectFile);

        protected TestCommand(MSBuildTest msBuild, string projectRootPath, bool skipResolvingProjectFile = false)
        {
            MSBuild = msBuild;
            ProjectRootPath = projectRootPath;

            if (!skipResolvingProjectFile)
            {
                ProjectFile = FindProjectFile();
            }
        }

        public abstract CommandResult Execute(params string[] args);

        private string FindProjectFile()
        {
            var buildProjectFiles = Directory.GetFiles(ProjectRootPath, "*.csproj");

            if(buildProjectFiles.Length != 1)
            {
                var errorMsg = $"Found {buildProjectFiles.Length} csproj files under {ProjectRootPath} instead of just 1.";
                throw new ArgumentException(errorMsg);
            }

            return buildProjectFiles[0];
        }
    }
}