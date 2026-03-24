// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.NET.TestFramework
{
    /// <summary>
    /// Skips a test when running on the specified platform, optionally restricted to a specific process architecture.
    /// </summary>
    public class SkipOnPlatformFact : FactAttribute
    {
        public SkipOnPlatformFact(TestPlatforms platforms, string issue)
        {
            if (PlatformsMatchCurrentOS(platforms))
            {
                Skip = issue;
            }
        }

        public SkipOnPlatformFact(TestPlatforms platforms, Architecture architecture, string issue)
        {
            if (PlatformsMatchCurrentOS(platforms) && RuntimeInformation.ProcessArchitecture == architecture)
            {
                Skip = issue;
            }
        }

        private static bool PlatformsMatchCurrentOS(TestPlatforms platforms) =>
            (platforms.HasFlag(TestPlatforms.Windows) && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                || (platforms.HasFlag(TestPlatforms.Linux) && RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                || (platforms.HasFlag(TestPlatforms.OSX) && RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                || (platforms.HasFlag(TestPlatforms.FreeBSD) && RuntimeInformation.IsOSPlatform(OSPlatform.Create("FREEBSD")));
    }
}
