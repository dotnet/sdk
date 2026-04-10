// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework
{
    /// <summary>
    /// Controls which platforms and architectures a test should run on or be skipped on.
    /// The constructor <paramref name="platforms"/> parameter specifies platforms to include (run on).
    /// Named properties <see cref="SkipPlatforms"/>, <see cref="Architecture"/>, and
    /// <see cref="SkipArchitecture"/> provide additional filtering.
    /// </summary>
    public class PlatformSpecificFact : FactAttribute
    {
        internal const Architecture NoArchitectureFilter = (Architecture)(-1);

        private readonly TestPlatforms _platforms;
        private string? _skip;

        public PlatformSpecificFact() : this(TestPlatforms.Any)
        {
        }

        public PlatformSpecificFact(TestPlatforms platforms)
        {
            _platforms = platforms;
        }

        /// <summary>
        /// Platforms to skip on, even if included by the constructor parameter.
        /// When <see cref="SkipArchitecture"/> is also set, both must match for the test to be skipped.
        /// </summary>
        public TestPlatforms SkipPlatforms { get; set; }

        /// <summary>
        /// Restrict the test to run only on this process architecture.
        /// Tests on other architectures are skipped.
        /// </summary>
        public Architecture Architecture { get; set; } = NoArchitectureFilter;

        /// <summary>
        /// Architecture to skip on. When <see cref="SkipPlatforms"/> is also set,
        /// both must match for the test to be skipped. When used alone, skips on the
        /// specified architecture regardless of platform.
        /// </summary>
        public Architecture SkipArchitecture { get; set; } = NoArchitectureFilter;

        /// <summary>
        /// Reason or tracking issue URL for why the test is skipped.
        /// Used as the Skip message when a skip condition matches.
        /// </summary>
        public string? SkipReason { get; set; }

        public override string? Skip
        {
            get => _skip ?? EvaluateSkip(_platforms, SkipPlatforms, Architecture, SkipArchitecture, SkipReason);
            set => _skip = value;
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
