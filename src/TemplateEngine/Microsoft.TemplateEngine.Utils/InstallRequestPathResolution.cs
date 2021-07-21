// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Utils
{
    /// <summary>
    /// Helper class which expands request like "*.nupkg" into multiple ".nupkg" files in folder.
    /// And returns absolute path to files or folders.
    /// </summary>
    public static class InstallRequestPathResolution
    {
        /// <summary>
        /// Returns absolute path to files or folders resolved from <paramref name="maskedPath"/> or unchanged <paramref name="maskedPath"/> when path cannot be resolved.
        /// </summary>
        /// <remarks>
        /// Example of <paramref name="maskedPath"/> would be "C:\Users\username\packages\*.nupkg".<br/>
        /// Wildcards are supported only in file name.
        /// Supported wildcards and rules are identical as for searchPattern for <see cref="Directory.EnumerateDirectories(string, string)"/>.
        /// </remarks>
        /// <param name="maskedPath">This parameter can contain a wildcard (*) character in the filename.</param>
        /// <param name="environmentSettings"></param>
        /// <returns>List of absolute paths to files or folders that match <paramref name="maskedPath"/> or unchanged <paramref name="maskedPath"/> when path cannot be resolved.</returns>
        public static IEnumerable<string> ExpandMaskedPath(string maskedPath, IEngineEnvironmentSettings environmentSettings)
        {
            if (string.IsNullOrWhiteSpace(maskedPath))
            {
                throw new ArgumentException($"'{nameof(maskedPath)}' cannot be null or whitespace.", nameof(maskedPath));
            }

            if (environmentSettings is null)
            {
                throw new ArgumentNullException(nameof(environmentSettings));
            }

            var arr = Path.GetInvalidPathChars();
            if (maskedPath.IndexOfAny(arr) != -1)
            {
                yield return maskedPath;
                yield break;
            }

            foreach (string path in ResolveSearchPattern(maskedPath, environmentSettings))
            {
                yield return path;
            }
        }

        private static IEnumerable<string> ResolveSearchPattern(string maskedPath, IEngineEnvironmentSettings environmentSettings)
        {
            string baseDir = maskedPath;

            //trim trailing separators
            if (baseDir[baseDir.Length - 1] == '/' || baseDir[baseDir.Length - 1] == '\\')
            {
                baseDir = baseDir.Substring(0, baseDir.Length - 1);
            }

            try
            {
                string searchTarget = Path.Combine(environmentSettings.Host.FileSystem.GetCurrentDirectory(), baseDir.Trim());
                string parentFolder = Path.GetFullPath(Path.GetDirectoryName(searchTarget));
                string searchPattern = Path.GetFileName(searchTarget);

                //EnumerateFileSystemEntries treats '.' as '*' and cannot handle '..'
                if (searchPattern.Equals(".") || searchPattern.Equals(".."))
                {
                    return new[] { Path.GetFullPath(searchTarget) };
                }
                IEnumerable<string> matches = environmentSettings.Host.FileSystem.EnumerateFileSystemEntries(parentFolder, searchPattern, SearchOption.TopDirectoryOnly);
                if (!matches.Any())
                {
                    return new[] { maskedPath };
                }
                return matches;
            }
            catch (Exception ex)
            {
                environmentSettings.Host.Logger.LogDebug("Failed to parse masked path {0}, {1}.", maskedPath, ex.ToString());
                return new[] { maskedPath };
            }
        }
    }
}
