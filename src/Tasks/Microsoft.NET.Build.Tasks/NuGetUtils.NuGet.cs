﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;

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

        public static bool IsApplicableAnalyzer(string file, string projectLanguage)
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

            return IsAnalyzer() && FileMatchesProjectLanguage();
        }
    }
}
