// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;

namespace Microsoft.NET.TestFramework.Utilities
{
    public static class DotnetVersionHelper
    {
        public static string GetPreviousDotnetVersion()
        {
            string currentFramework = ToolsetInfo.CurrentTargetFramework;
            var match = Regex.Match(currentFramework, @"^net(\d+)\.(\d+)$");
            if (!match.Success)
            {
                throw new InvalidOperationException($"Invalid target framework format: {currentFramework}");
            }

            int majorVersion = int.Parse(match.Groups[1].Value);

            if (majorVersion > 0)
            {
                majorVersion--;
            }
            else
            {
                throw new InvalidOperationException($"Cannot determine previous version for target framework: {currentFramework}");
            }

            return $"net{majorVersion}.0";
        }
    }
}
