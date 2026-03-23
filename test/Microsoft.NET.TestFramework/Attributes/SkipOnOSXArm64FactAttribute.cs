// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework
{
    /// <summary>
    /// Skips a test when running on macOS with an arm64 process architecture.
    /// </summary>
    public class SkipOnOSXArm64FactAttribute : FactAttribute
    {
        public SkipOnOSXArm64FactAttribute(string reason = "This test is not supported on macOS arm64.")
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                Skip = reason;
            }
        }
    }
}
