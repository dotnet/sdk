// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.IO;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Utils
{
    public static class IFileSystemInfoExtensions
    {
        /// <summary>
        /// Returns full path to <paramref name="fileSystemInfo"/> including mount point URI in a format
        /// that is suitable for displaying in the CLI or printing to logs.
        /// This method wraps the path with quotes if it contains spaces.
        /// </summary>
        /// <param name="fileSystemInfo">the file system info to get full path for.</param>
        /// <returns>
        /// Full path to <paramref name="fileSystemInfo"/> including mount point URI.
        /// If mount point is not a directory, the path is returned as 'mount point URI(path inside mount point)'.
        /// </returns>
        public static string GetDisplayPath(this IFileSystemInfo fileSystemInfo)
        {
            string result = string.Empty;
            if (fileSystemInfo.MountPoint.EnvironmentSettings.Host.FileSystem.DirectoryExists(fileSystemInfo.MountPoint.MountPointUri))
            {
                //mount point is a directory, combine paths
                result = Path.Combine(fileSystemInfo.MountPoint.MountPointUri, fileSystemInfo.FullPath.Trim('/', '\\'));
            }
            else
            {
                //assuming file or anything else
                result = $"{fileSystemInfo.MountPoint.MountPointUri}({fileSystemInfo.FullPath})";
            }

            if (result.Contains(" "))
            {
                result = '"' + result + '"';
            }

            return result;
        }
    }
}
