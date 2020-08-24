// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using Xunit;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public class PlatformSpecificFact : FactAttribute
    {
        public PlatformSpecificFact(TestPlatforms platforms)
        {
            if (ShouldSkip(platforms))
            {
                this.Skip = "This test is not supported on this platform.";
            }
        }

        internal static bool ShouldSkip(TestPlatforms platforms) =>
            (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !platforms.HasFlag(TestPlatforms.Windows))
                || (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && !platforms.HasFlag(TestPlatforms.Linux))
                || (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && !platforms.HasFlag(TestPlatforms.OSX))
                || (RuntimeInformation.IsOSPlatform(OSPlatform.Create("FREEBSD")) && !platforms.HasFlag(TestPlatforms.FreeBSD));
    }

    [Flags]
    public enum TestPlatforms
    {
        Any = -1,
        Windows = 1,
        Linux = 2,
        OSX = 4,
        FreeBSD = 8,
        NetBSD = 16,
        illumos = 32,
        Solaris = 64,
        iOS = 128,
        tvOS = 256,
        Android = 512,
        Browser = 1024,
        AnyUnix = 2048
    }
}
