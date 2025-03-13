// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.NET.TestFramework
{
    public class PlatformAndArchitectureSpecificTheory : TheoryAttribute
    {
        public PlatformAndArchitectureSpecificTheory(TestPlatforms platforms, Architecture architectures, bool requireBoth)
        {
            if (requireBoth)
            {
                if (PlatformSpecificFact.ShouldSkip(platforms))
                {
                    Skip = "This test is not supported on this platform";
                }

                if (PlatformAndArchitectureSpecificFact.ShouldSkip(architectures))
                {
                    Skip = string.IsNullOrEmpty(Skip) ? "This test is not supported on this architecture" : Skip + " or architecture";
                }

                if (!string.IsNullOrEmpty(Skip))
                {
                    Skip += '.';
                }
            }
            else
            {
                if (PlatformSpecificFact.ShouldSkip(platforms) && PlatformAndArchitectureSpecificFact.ShouldSkip(architectures))
                {
                    Skip = "This test is not supported on this platform/architecture combination.";
                }
            }
        }
    }
}
