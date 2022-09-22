// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.TestHelper
{
    public static class TestFileSystemUtils
    {
        public static readonly string DefaultConfigRelativePath = ".template.config/template.json";

        /// <summary>
        /// Returns the temp directory path that has been virtualized.
        /// </summary>
        public static string GetTempVirtualizedPath(this IEngineEnvironmentSettings environmentSettings)
        {
            string basePath = Path.Combine(Directory.GetCurrentDirectory(), "sandbox", Guid.NewGuid().ToString()) + Path.DirectorySeparatorChar;
            environmentSettings.Host.VirtualizeDirectory(basePath);

            return basePath;
        }

        /// <summary>
        /// Writes the file <paramref name="filePath"/> with content <paramref name="fileContent"/>.
        /// </summary>
        public static void WriteFile(this IEngineEnvironmentSettings environmentSettings, string filePath, string? fileContent)
        {
            string fullPathDir = Path.GetDirectoryName(filePath) ?? throw new ArgumentException("Failed to get directory name.", nameof(filePath));

            environmentSettings.Host.FileSystem.CreateDirectory(fullPathDir);
            environmentSettings.Host.FileSystem.WriteAllText(filePath, fileContent ?? string.Empty);
        }

        /// <summary>
        /// Writes the <paramref name="templateSourceFileNamesWithContent"/> to <paramref name="sourceBasePath"/>.
        /// </summary>
        public static void WriteTemplateSource(
            this IEngineEnvironmentSettings environmentSettings,
            string sourceBasePath,
            IDictionary<string, string?> templateSourceFileNamesWithContent)
        {
            foreach (KeyValuePair<string, string?> fileInfo in templateSourceFileNamesWithContent)
            {
                string filePath = Path.Combine(sourceBasePath, fileInfo.Key);
                WriteFile(environmentSettings, filePath, fileInfo.Value);
            }
        }

        /// <summary>
        /// Mounts the <paramref name="sourceBasePath"/> and returns the mount point.
        /// Note that <see cref="IMountPoint"/> is disposable and needs to be disposed after use.
        /// </summary>
        /// <exception cref="InvalidOperationException">when mount succeeded but returned <see cref="IMountPoint"/> is <see langword="null"/>.</exception>
        /// <exception cref="Exception">when the path failed to be mounted.</exception>
        public static IMountPoint MountPath(this IEngineEnvironmentSettings environmentSettings, string sourceBasePath)
        {
            foreach (IMountPointFactory factory in environmentSettings.Components.OfType<IMountPointFactory>())
            {
                if (factory.TryMount(environmentSettings, null, sourceBasePath, out IMountPoint? sourceMountPoint))
                {
                    if (sourceMountPoint is null)
                    {
                        throw new InvalidOperationException($"{nameof(sourceMountPoint)} cannot be null when {nameof(factory.TryMount)} is 'true'.");
                    }
                    return sourceMountPoint;
                }
            }
            throw new Exception($"Failed to mount path {sourceBasePath}.");
        }
    }
}
