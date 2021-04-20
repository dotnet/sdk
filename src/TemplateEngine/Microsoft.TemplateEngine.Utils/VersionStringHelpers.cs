// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Utils
{
    public static class VersionStringHelpers
    {
        public static bool TryParseVersionSpecification(string versionString, out IVersionSpecification specification)
        {
            if (string.IsNullOrEmpty(versionString))
            {
                specification = null;
                return false;
            }
            else if (versionString.Contains("-"))
            {
                return RangeVersionSpecification.TryParse(versionString, out specification);
            }
            else
            {
                return ExactVersionSpecification.TryParse(versionString, out specification);
            }
        }

        // returns the relative order of the versions:
        // null if either is not a valid version.
        // -1 if version1 < version2
        // 0 if version1 == version2
        // 1 if version1 > version2
        public static int? CompareVersions(string version1, string version2)
        {
            if (!TryParseVersionString(version1, out int[] parts1) || !TryParseVersionString(version2, out int[] parts2))
            {
                return null;
            }

            for (int i = 0; i < 4; i++)
            {
                if (parts1[i] > parts2[i])
                {
                    return 1;
                }
                else if (parts1[i] < parts2[i])
                {
                    return -1;
                }
            }

            return 0;
        }

        public static bool IsVersionWellFormed(string version)
        {
            return TryParseVersionString(version, out int[] parsed);
        }

        // tries to parse a version into 4 int parts, zero-padding on the rght if needed.
        // more than 4 parts, return false. 
        // Unparse-able, return false.
        private static bool TryParseVersionString(string version, out int[] parsed)
        {
            if (string.IsNullOrEmpty(version))
            {
                parsed = null;
                return false;
            }

            string[] parts = version.Split(new[] { '.' });
            if (parts.Length < 2 || parts.Length > 4)
            {
                parsed = null;
                return false;
            }

            parsed = new[] { 0, 0, 0, 0 };

            for (int i = 0; i < parts.Length; i++)
            {
                if (int.TryParse(parts[i], out int intPart))
                {
                    parsed[i] = intPart;
                }
                else
                {
                    parsed = null;
                    return false;
                }
            }

            return true;
        }
    }
}
