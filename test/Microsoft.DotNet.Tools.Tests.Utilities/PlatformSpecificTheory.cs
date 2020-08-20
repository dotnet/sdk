// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;

namespace Microsoft.DotNet.Tools.Test.Utilities
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
