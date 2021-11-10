// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using Xunit;

namespace Microsoft.NET.TestFramework
{
    public class AOTSupportedEnvironmentsOnlyFactAttribute : FactAttribute
    {
        public AOTSupportedEnvironmentsOnlyFactAttribute(string msBuildVersion)
        {
            // Enforce minimum MSBuild version
            RequiresMSBuildVersionTheoryAttribute.CheckForRequiredMSBuildVersion(this, msBuildVersion);

            // Skip tests on Linux/Ubuntu
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                this.Skip = @"Ubuntu AOT testing isn't currently possible due to an out of date python in the CI image which doesn't support f-string. 
Consequently the emcc.py script is failing during AOT. This bit should be reverted when the CI Ubuntu image is upgraded to 20.04 LTS.";
            }
        }
    }
}
