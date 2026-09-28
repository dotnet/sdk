// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework
{
    [Flags]
    public enum TestArchitectures
    {
        None = 0,
        X64 = 1,
        ARM64 = 2,
        All = ~0
    }

    public class PlatformSpecificFact : FactAttribute
    {
        public PlatformSpecificFact(TestPlatforms platforms, TestArchitectures architectures = TestArchitectures.All)
        {
            if (ShouldSkip(platforms))
            {
                Skip = "This test is not supported on this platform.";
            }
            else if (ShouldSkipArchitecture(architectures))
            {
                Skip = "This test is not supported on this architecture.";
            }
        }

        internal static bool ShouldSkip(TestPlatforms platforms) =>
            (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !platforms.HasFlag(TestPlatforms.Windows))
                || (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && !platforms.HasFlag(TestPlatforms.Linux))
                || (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && !platforms.HasFlag(TestPlatforms.OSX))
                || (RuntimeInformation.IsOSPlatform(OSPlatform.Create("FREEBSD")) && !platforms.HasFlag(TestPlatforms.FreeBSD));

        internal static bool ShouldSkipArchitecture(TestArchitectures architectures) =>
            architectures != TestArchitectures.All &&
            RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => !architectures.HasFlag(TestArchitectures.X64),
                Architecture.Arm64 => !architectures.HasFlag(TestArchitectures.ARM64),
                _ => true,
            };
    }
}
