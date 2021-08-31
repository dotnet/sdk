// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.RuntimeModel;

namespace Microsoft.NET.Build.Tasks
{
    internal static partial class NuGetUtils
    {
        public static bool IsPlaceholderFile(string path)
        {
            // PERF: avoid allocations here as we check this for every file in project.assets.json
            if (!path.EndsWith("_._", StringComparison.Ordinal))
            {
                return false;
            }

            if (path.Length == 3)
            {
                return true;
            }

            char separator = path[path.Length - 4];
            return separator == '\\' || separator == '/';
        }

        public static string GetLockFileLanguageName(string projectLanguage)
        {
            switch (projectLanguage)
            {
                case "C#": return "cs";
                case "F#": return "fs";
                default: return projectLanguage?.ToLowerInvariant();
            }
        }

        public static NuGetFramework ParseFrameworkName(string frameworkName)
        {
            return frameworkName == null ? null : NuGetFramework.Parse(frameworkName);
        }

        /// <summary>
        /// Gets a collection of 'analyzer' assets that should be excluded based on the
        /// <paramref name="compilerApiVersion"/>.
        /// </summary>
        /// <remarks>
        /// This allows packages to ship multiple analyzers that target different versions
        /// of the compiler. For example, a package may include:
        ///
        /// "analyzers/dotnet/roslyn3.7/analyzer.dll"
        /// "analyzers/dotnet/roslyn3.8/analyzer.dll"
        /// "analyzers/dotnet/roslyn4.0/analyzer.dll"
        ///
        /// When the <paramref name="compilerApiVersion"/> is 'roslyn3.9', only the assets 
        /// in the folder with the highest applicable compiler version are picked.
        /// In this case,
        /// 
        /// "analyzers/dotnet/roslyn3.8/analyzer.dll"
        /// 
        /// will be picked, and the other analyzer assets will be excluded.
        ///
        /// "analyzers/dotnet/roslyn3.7/analyzer.dll"
        /// "analyzers/dotnet/roslyn4.0/analyzer.dll"
        /// 
        /// will be returned, since they should be excluded.
        /// </remarks>
        public static HashSet<string>? GetExcludedAnalyzers(LockFile lockFile, string projectLanguage, string compilerApiVersion)
        {
            if (!ParseCompilerApiVersion(compilerApiVersion, out ReadOnlyMemory<char> compilerName, out Version compilerVersion))
            {
                return null;
            }

            HashSet<string> excludedAnalyzers = null;

            // gather all the potential analyzers contained in a folder for the current compiler
            Version maxApplicableVersion = null;
            List<(string, Version)> potentialAnalyzers = null;

#if NETFRAMEWORK
            string compilerSearchString = "/" + compilerName;
#else
            string compilerSearchString = string.Concat("/".AsSpan(), compilerName.Span);
#endif
            foreach (var library in lockFile.Libraries)
            {
                if (!library.IsPackage())
                {
                    continue;
                }

                foreach (var file in library.Files)
                {
                    if (IsApplicableAnalyzer(file, projectLanguage, excludedAnalyzers: null))
                    {
                        int compilerNameStart = file.IndexOf(compilerSearchString);
                        if (compilerNameStart == -1)
                        {
                            continue;
                        }

                        int compilerVersionStart = compilerNameStart + compilerSearchString.Length;
                        int compilerVersionStop = file.IndexOf('/', compilerVersionStart);
                        if (compilerVersionStop == -1)
                        {
                            continue;
                        }

                        if (!TryParseVersion(file, compilerVersionStart, compilerVersionStop - compilerVersionStart, out Version fileCompilerVersion))
                        {
                            continue;
                        }

                        // version is too high - add to exclude list
                        if (fileCompilerVersion > compilerVersion)
                        {
                            excludedAnalyzers ??= new HashSet<string>();
                            excludedAnalyzers.Add(file);
                        }
                        else
                        {
                            potentialAnalyzers ??= new List<(string, Version)>();
                            potentialAnalyzers.Add((file, fileCompilerVersion));

                            if (maxApplicableVersion == null || fileCompilerVersion > maxApplicableVersion)
                            {
                                maxApplicableVersion = fileCompilerVersion;
                            }
                        }
                    }
                }

                if (maxApplicableVersion != null && potentialAnalyzers?.Count > 0)
                {
                    foreach (var (file, version) in potentialAnalyzers)
                    {
                        if (version != maxApplicableVersion)
                        {
                            excludedAnalyzers ??= new HashSet<string>();
                            excludedAnalyzers.Add(file);
                        }
                    }
                }

                maxApplicableVersion = null;
                potentialAnalyzers?.Clear();
            }

            return excludedAnalyzers;
        }

        /// <summary>
        /// Parses the <paramref name="compilerApiVersion"/> string into its component parts:
        /// compilerName:, e.g. "roslyn"
        /// compilerVersion: e.g. 3.9
        /// </summary>
        private static bool ParseCompilerApiVersion(string compilerApiVersion, out ReadOnlyMemory<char> compilerName, out Version compilerVersion)
        {
            compilerName = default;
            compilerVersion = default;

            if (string.IsNullOrEmpty(compilerApiVersion))
            {
                return false;
            }

            int compilerVersionStart = -1;
            for (int i = 0; i < compilerApiVersion.Length; i++)
            {
                if (char.IsDigit(compilerApiVersion[i]))
                {
                    compilerVersionStart = i;
                    break;
                }
            }

            if (compilerVersionStart > 0)
            {
                if (TryParseVersion(compilerApiVersion, compilerVersionStart, out compilerVersion))
                {
                    compilerName = compilerApiVersion.AsMemory(0, compilerVersionStart);
                    return true;
                }
            }

            // didn't find a compiler name or version
            return false;
        }

        private static bool TryParseVersion(string value, int startIndex, out Version version) =>
            TryParseVersion(value, startIndex, value.Length - startIndex, out version);

        private static bool TryParseVersion(string value, int startIndex, int length, out Version version)
        {
#if NETFRAMEWORK
            return Version.TryParse(value.Substring(startIndex, length), out version);
#else
            return Version.TryParse(value.AsSpan(startIndex, length), out version);
#endif
        }

        public static bool IsApplicableAnalyzer(string file, string projectLanguage, HashSet<string>? excludedAnalyzers)
        {
            // This logic is preserved from previous implementations.
            // See https://github.com/NuGet/Home/issues/6279#issuecomment-353696160 for possible issues with it.

            bool IsAnalyzer()
            {
                return file.StartsWith("analyzers", StringComparison.Ordinal)
                    && file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    && !file.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase);
            }

            bool CS() => file.IndexOf("/cs/", StringComparison.OrdinalIgnoreCase) >= 0;
            bool VB() => file.IndexOf("/vb/", StringComparison.OrdinalIgnoreCase) >= 0;

            bool FileMatchesProjectLanguage()
            {
                switch (projectLanguage)
                {
                    case "C#":
                        return CS() || !VB();

                    case "VB":
                        return VB() || !CS();

                    default:
                        return false;
                }
            }

            return IsAnalyzer() && FileMatchesProjectLanguage() && excludedAnalyzers?.Contains(file) != true;
        }

        public static string GetBestMatchingRid(RuntimeGraph runtimeGraph, string runtimeIdentifier,
            IEnumerable<string> availableRuntimeIdentifiers, out bool wasInGraph)
        {
            wasInGraph = runtimeGraph.Runtimes.ContainsKey(runtimeIdentifier);

            HashSet<string> availableRids = new HashSet<string>(availableRuntimeIdentifiers);
            foreach (var candidateRuntimeIdentifier in runtimeGraph.ExpandRuntime(runtimeIdentifier))
            {
                if (availableRids.Contains(candidateRuntimeIdentifier))
                {
                    return candidateRuntimeIdentifier;
                }
            }

            //  No compatible RID found in availableRuntimeIdentifiers
            return null;
        }
    }
}
