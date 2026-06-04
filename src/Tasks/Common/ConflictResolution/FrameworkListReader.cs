// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Reflection;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks.ConflictResolution
{
    class FrameworkListReader
    {
        private static readonly object s_cacheLock = new();

        private IBuildEngine4 _buildEngine;

        public FrameworkListReader(IBuildEngine4 buildEngine)
        {
            _buildEngine = buildEngine;
        }

        public IEnumerable<ConflictItem> GetConflictItems(AbsolutePath frameworkListPath, Logger log)
        {
            if (!Path.IsPathRooted(frameworkListPath.OriginalValue))
            {
                throw new BuildErrorException(Strings.FrameworkListPathNotRooted, frameworkListPath.OriginalValue);
            }

            //  Need to include assembly name in the key here, since both Microsoft.NET.Build.Tasks and Microsoft.NET.Build.Extensions.Tasks share this code,
            //  but can't share the types of the ConflictItem objects.
            string? assemblyName = typeof(FrameworkListReader).GetTypeInfo().Assembly.FullName;

            string objectKey = $"{assemblyName}:{nameof(FrameworkListReader)}:{frameworkListPath.OriginalValue}";

            IEnumerable<ConflictItem> result;

            lock (s_cacheLock)
            {
                object existingConflictItems = _buildEngine.GetRegisteredTaskObject(objectKey, RegisteredTaskObjectLifetime.AppDomain);

                if (existingConflictItems == null)
                {
                    result = LoadConflictItems(frameworkListPath, log);

                    _buildEngine.RegisterTaskObject(objectKey, result, RegisteredTaskObjectLifetime.AppDomain, true);
                }
                else
                {
                    result = (IEnumerable<ConflictItem>)existingConflictItems;
                }
            }

            return result;
        }

        private static IEnumerable<ConflictItem> LoadConflictItems(AbsolutePath frameworkListPath, Logger log)
        {
            if (!File.Exists(frameworkListPath))
            {
                //  This is not an error, as we get both the root target framework directory as well as the Facades folder passed in as TargetFrameworkDirectories.
                //  Only the root will have a RedistList\FrameworkList.xml in it
                return Enumerable.Empty<ConflictItem>();
            }

            var frameworkList = XDocument.Load(frameworkListPath);
            var ret = new List<ConflictItem>();
            foreach (var file in frameworkList.Root?.Elements("File") ?? [])
            {
                var type = file.Attribute("Type")?.Value;

                if (type?.Equals("Analyzer", StringComparison.OrdinalIgnoreCase) ?? false)
                {
                    continue;
                }

                var assemblyName = file.Attribute("AssemblyName")?.Value;
                var assemblyVersionString = file.Attribute("Version")?.Value;

                if (string.IsNullOrEmpty(assemblyName))
                {
                    string errorMessage = string.Format(CultureInfo.CurrentCulture, Strings.ErrorParsingFrameworkListInvalidValue,
                        frameworkListPath.OriginalValue,
                        "AssemblyName",
                        assemblyName);
                    log.LogError(errorMessage);
                    return Enumerable.Empty<ConflictItem>();
                }

                Version? assemblyVersion;
                if (string.IsNullOrEmpty(assemblyVersionString) || !Version.TryParse(assemblyVersionString, out assemblyVersion))
                {
                    string errorMessage = string.Format(CultureInfo.CurrentCulture, Strings.ErrorParsingFrameworkListInvalidValue,
                        frameworkListPath.OriginalValue,
                        "Version",
                        assemblyVersionString);
                    log.LogError(errorMessage);
                    return Enumerable.Empty<ConflictItem>();
                }

                ret.Add(new ConflictItem(assemblyName + ".dll",
                                                packageId: "TargetingPack",
                                                assemblyVersion: assemblyVersion,
                                                fileVersion: null));
            }

            return ret;
        }
    }
}
