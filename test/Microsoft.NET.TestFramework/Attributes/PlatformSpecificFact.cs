// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.NET.TestFramework
{
    /// <summary>
    /// Controls which platforms and architectures a test should run on or be skipped on.
    /// <paramref name="platforms"/> specifies platforms to include (run on).
    /// Optional parameters provide additional skip-based filtering.
    /// </summary>
    public class PlatformSpecificFact : FactAttribute
    {
        internal const Architecture NoArchitectureFilter = (Architecture)(-1);

        public PlatformSpecificFact(
            TestPlatforms platforms = TestPlatforms.Any,
            TestPlatforms skipPlatforms = 0,
            Architecture architecture = NoArchitectureFilter,
            Architecture skipArchitecture = NoArchitectureFilter,
            string? skipReason = null,
            [CallerFilePath] string? sourceFilePath = null,
            [CallerLineNumber] int sourceLineNumber = 0)
            : base(sourceFilePath, sourceLineNumber)
        {
            Skip = EvaluateSkip(platforms, skipPlatforms, architecture, skipArchitecture, skipReason);
        }

        internal static string? EvaluateSkip(
            TestPlatforms platforms,
            TestPlatforms skipPlatforms,
            Architecture architecture,
            Architecture skipArchitecture,
            string? skipReason)
        {
            // Check include platform list
            if (!PlatformsMatchCurrentOS(platforms))
            {
                return "This test is not supported on this platform.";
            }

            // Check include architecture
            if (architecture != NoArchitectureFilter && RuntimeInformation.ProcessArchitecture != architecture)
            {
                return $"This test is not supported on {RuntimeInformation.ProcessArchitecture} architecture.";
            }

            // Check skip platform + skip architecture (combined with AND when both set)
            bool skipPlatformMatches = skipPlatforms != 0 && PlatformsMatchCurrentOS(skipPlatforms);
            bool skipArchMatches = skipArchitecture != NoArchitectureFilter && RuntimeInformation.ProcessArchitecture == skipArchitecture;

            if (skipPlatforms != 0 && skipArchitecture != NoArchitectureFilter)
            {
                // Both specified: skip only when both match
                if (skipPlatformMatches && skipArchMatches)
                {
                    return skipReason ?? "Test skipped on this platform and architecture.";
                }
            }
            else if (skipPlatformMatches)
            {
                return skipReason ?? "Test skipped on this platform.";
            }
            else if (skipArchMatches)
            {
                return skipReason ?? "Test skipped on this architecture.";
            }

            return null;
        }

        private static bool PlatformsMatchCurrentOS(TestPlatforms platforms) =>
            (platforms.HasFlag(TestPlatforms.Windows) && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                || (platforms.HasFlag(TestPlatforms.Linux) && RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                || (platforms.HasFlag(TestPlatforms.OSX) && RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                || (platforms.HasFlag(TestPlatforms.FreeBSD) && RuntimeInformation.IsOSPlatform(OSPlatform.Create("FREEBSD")));
    }
}
