// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.NET.TestFramework
{
    /// <summary>
    /// Skips a test when running on macOS with an ARM64 process architecture.
    /// </summary>
    public class SkipOnMacOSArm64FactAttribute : FactAttribute
    {
        public SkipOnMacOSArm64FactAttribute(string issue)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) &&
                RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                Skip = issue;
            }
        }
    }
}
