// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework
{
    public class RequiresMSBuildVersionFactAttribute : FactAttribute
    {
        public RequiresMSBuildVersionFactAttribute(string version)
        {
            RequiresMSBuildVersionTheoryAttribute.CheckForRequiredMSBuildVersion(this, version);
        }
    }
}
