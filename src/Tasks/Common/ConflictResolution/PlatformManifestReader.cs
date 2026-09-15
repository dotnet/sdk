// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Reflection;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks.ConflictResolution
{
    static class PlatformManifestReader
    {
        private static readonly object s_cacheLock = new();
        static readonly char[] s_manifestLineSeparator = new[] { '|' };

        public static IEnumerable<ConflictItem> LoadConflictItems(AbsolutePath? manifestPath, Logger log, IBuildEngine4 buildEngine)
        {
            if (manifestPath is not AbsolutePath path)
            {
                throw new ArgumentNullException(nameof(manifestPath));
            }

            // Both task assemblies compile this shared source but cannot share their ConflictItem types.
            string? assemblyName = typeof(PlatformManifestReader).GetTypeInfo().Assembly.FullName;
            string objectKey = $"{assemblyName}:{nameof(PlatformManifestReader)}:{path.Value}";

            lock (s_cacheLock)
            {
                if (buildEngine.GetRegisteredTaskObject(objectKey, RegisteredTaskObjectLifetime.Build) is ConflictItem[] conflictItems)
                {
                    return conflictItems;
                }

                conflictItems = LoadConflictItems(path, log, out bool succeeded);
                if (succeeded)
                {
                    buildEngine.RegisterTaskObject(
                        objectKey,
                        conflictItems,
                        RegisteredTaskObjectLifetime.Build,
                        allowEarlyCollection: false);
                }

                return conflictItems;
            }
        }

        private static ConflictItem[] LoadConflictItems(AbsolutePath path, Logger log, out bool succeeded)
        {
            succeeded = true;

            if (!File.Exists(path))
            {
                string errorMessage = string.Format(CultureInfo.CurrentCulture, Strings.CouldNotLoadPlatformManifest,
                    path.OriginalValue);
                log.LogError(errorMessage);
                succeeded = false;
                return Array.Empty<ConflictItem>();
            }

            var conflictItems = new List<ConflictItem>();
            using (var manifestStream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
            using (var manifestReader = new StreamReader(manifestStream))
            {
                for (int lineNumber = 0; !manifestReader.EndOfStream; lineNumber++)
                {
                    var line = manifestReader.ReadLine()?.Trim();

                    if (line is null || line.Length == 0 || line[0] == '#')
                    {
                        continue;
                    }

                    var lineParts = line.Split(s_manifestLineSeparator);

                    if (lineParts.Length != 4)
                    {
                        string errorMessage = string.Format(CultureInfo.CurrentCulture, Strings.ErrorParsingPlatformManifest,
                            path.OriginalValue,
                            lineNumber,
                            "fileName|packageId|assemblyVersion|fileVersion");
                        log.LogError(errorMessage);
                        succeeded = false;
                        return conflictItems.ToArray();
                    }

                    var fileName = lineParts[0].Trim();
                    var packageId = lineParts[1].Trim();
                    var assemblyVersionString = lineParts[2].Trim();
                    var fileVersionString = lineParts[3].Trim();

                    Version? assemblyVersion = null, fileVersion = null;

                    if (assemblyVersionString.Length != 0 && !Version.TryParse(assemblyVersionString, out assemblyVersion))
                    {
                        string errorMessage = string.Format(CultureInfo.CurrentCulture, Strings.ErrorParsingPlatformManifestInvalidValue,
                            path.OriginalValue,
                            lineNumber,
                            "AssemblyVersion",
                            assemblyVersionString);
                        log.LogError(errorMessage);
                        succeeded = false;
                    }

                    if (fileVersionString.Length != 0 && !Version.TryParse(fileVersionString, out fileVersion))
                    {
                        string errorMessage = string.Format(CultureInfo.CurrentCulture, Strings.ErrorParsingPlatformManifestInvalidValue,
                            path.OriginalValue,
                            lineNumber,
                            "FileVersion",
                            fileVersionString);
                        log.LogError(errorMessage);
                        succeeded = false;
                    }

                    conflictItems.Add(new ConflictItem(fileName, packageId, assemblyVersion, fileVersion));
                }
            }

            return conflictItems.ToArray();
        }
    }
}
