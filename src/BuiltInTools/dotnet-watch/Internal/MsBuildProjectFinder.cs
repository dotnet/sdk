﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Watcher.Tools;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Internal
{
    internal class MsBuildProjectFinder
    {
        /// <summary>
        /// Finds a compatible MSBuild project.
        /// <param name="searchBase">The base directory to search</param>
        /// <param name="project">The filename of the project. Can be null.</param>
        /// </summary>
        public static string FindMsBuildProject(string searchBase, string project)
        {
            Ensure.NotNullOrEmpty(searchBase, nameof(searchBase));

            var projectPath = project ?? searchBase;

            if (!Path.IsPathRooted(projectPath))
            {
                projectPath = Path.Combine(searchBase, projectPath);
            }

            if (Directory.Exists(projectPath))
            {
                var projects = Directory.EnumerateFileSystemEntries(projectPath, "*.*proj", SearchOption.TopDirectoryOnly)
                    .Where(f => !".xproj".Equals(Path.GetExtension(f), StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (projects.Count > 1)
                {
                    throw new FileNotFoundException(string.Format(CultureInfo.CurrentCulture, Resources.Error_MultipleProjectsFound, projectPath));
                }

                if (projects.Count == 0)
                {
                    throw new FileNotFoundException(string.Format(CultureInfo.CurrentCulture, Resources.Error_NoProjectsFound, projectPath));
                }

                return projects[0];
            }

            if (!File.Exists(projectPath))
            {
                throw new FileNotFoundException(string.Format(CultureInfo.CurrentCulture, Resources.Error_ProjectPath_NotFound, projectPath));
            }

            return projectPath;
        }
    }
}
