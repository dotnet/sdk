// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Utils
{
    /// <summary>
    /// Helper class which expands request like "*.nupkg" into multiple ".nupkg" files in folder.
    /// And returns absolute path to files or folders.
    /// </summary>
    public static class InstallRequestPathResolution
    {
        private static IReadOnlyList<string> DetermineDirectoriesToScan(string baseDir, IEngineEnvironmentSettings environmentSettings)
        {
            List<string> directoriesToScan = new List<string>();

            if (baseDir[baseDir.Length - 1] == '/' || baseDir[baseDir.Length - 1] == '\\')
            {
                baseDir = baseDir.Substring(0, baseDir.Length - 1);
            }

            string searchTarget = Path.Combine(environmentSettings.Host.FileSystem.GetCurrentDirectory(), baseDir.Trim());
            List<string> matches = environmentSettings.Host.FileSystem.EnumerateFileSystemEntries(Path.GetDirectoryName(searchTarget), Path.GetFileName(searchTarget), SearchOption.TopDirectoryOnly).ToList();

            if (matches.Count == 1)
            {
                directoriesToScan.Add(matches[0]);
            }
            else
            {
                foreach (string match in matches)
                {
                    IReadOnlyList<string> subDirMatches = DetermineDirectoriesToScan(match, environmentSettings);
                    directoriesToScan.AddRange(subDirMatches);
                }
            }

            return directoriesToScan;
        }

        /// <summary>
        /// Returns absolute path to files or folders resolved from <paramref name="maskedPath"/>.
        /// </summary>
        /// <remarks>
        /// Example of <paramref name="maskedPath"/> would be "C:\Users\username\packages\*.nupkg".<br/>
        /// Wildcards are supported only in file name.
        /// Supported wildcards and rules are identical as for <see cref="searchPattern"/> for <see cref="Directory.EnumerateDirectories(string, string)"/>.
        /// </remarks>
        /// <param name="maskedPath">This parameter can contain a wildcard (*) character in the filename.</param>
        /// <param name="environmentSettings"></param>
        /// <returns>List of absolute paths to files or folders that match <paramref name="maskedPath"/>.</returns>
        public static IEnumerable<string> ExpandMaskedPath(string maskedPath, IEngineEnvironmentSettings environmentSettings)
        {
            if (maskedPath.IndexOfAny(Path.GetInvalidPathChars()) != -1)
            {
                yield return maskedPath;
                yield break;
            }
            var matches = DetermineDirectoriesToScan(maskedPath, environmentSettings).ToList();
            //This can happen when user specifies "PackageId"
            if (matches.Count == 0)
            {
                yield return maskedPath;
                yield break;
            }
            foreach (var path in matches)
            {
                yield return Path.GetFullPath(path);
            }
        }
    }
}
