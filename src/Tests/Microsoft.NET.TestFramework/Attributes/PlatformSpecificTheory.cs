// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace Microsoft.NET.TestFramework
{
    public class PlatformSpecificTheory : TheoryAttribute
    {
        public PlatformSpecificTheory(TestPlatforms platforms)
        {
            if (PlatformSpecificFact.ShouldSkip(platforms))
            {
                this.Skip = "This test is not supported on this platform.";
            }
        }
    }
}
