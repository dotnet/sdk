// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    static partial class FileUtilities
    {
        // Cache file versions across the build keyed by path + last-write time. The same
        // framework/package assemblies are referenced by many projects, so without this each
        // project's GenerateDepsFile re-opens and re-parses the Win32 version resource of the
        // same files. Mirrors the existing s_versionCache used for assembly versions and is
        // safe under multithreaded (-mt) builds (ConcurrentDictionary, immutable cache entries).
        private static readonly ConcurrentDictionary<string, (DateTime LastKnownWriteTimeUtc, Version? Version)> s_fileVersionCache
            = new(StringComparer.OrdinalIgnoreCase);

        public static Version? GetFileVersion(string? sourcePath)
        {
            if (sourcePath != null)
            {
                DateTime lastWriteTimeUtc = File.GetLastWriteTimeUtc(sourcePath);

                if (s_fileVersionCache.TryGetValue(sourcePath, out var cacheEntry)
                    && lastWriteTimeUtc == cacheEntry.LastKnownWriteTimeUtc)
                {
                    return cacheEntry.Version;
                }

                Version? version = null;
                var fvi = FileVersionInfo.GetVersionInfo(sourcePath);
                if (fvi != null)
                {
                    version = new Version(fvi.FileMajorPart, fvi.FileMinorPart, fvi.FileBuildPart, fvi.FilePrivatePart);
                }

                s_fileVersionCache[sourcePath] = (lastWriteTimeUtc, version);
                return version;
            }

            return null;
        }

        public static Version? GetFileVersion(AbsolutePath sourcePath) => GetFileVersion(sourcePath.Value);

        static readonly HashSet<string?> s_assemblyExtensions = new(new[] { ".dll", ".exe", ".winmd" }, StringComparer.OrdinalIgnoreCase);
        public static Version? TryGetAssemblyVersion(string sourcePath)
        {
            var extension = Path.GetExtension(sourcePath);

            return s_assemblyExtensions.Contains(extension) ? GetAssemblyVersion(sourcePath) : null;
        }

        public static Version? TryGetAssemblyVersion(AbsolutePath sourcePath) => TryGetAssemblyVersion(sourcePath.Value);
    }
}
