// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework
{
    public class WindowsOnlyRequiresMSBuildVersionFactAttribute : FactAttribute
    {
        /// <summary>
        /// Gets or sets the reason for potentially skipping the test if conditions are not met.
        /// </summary>
        public string Reason { get; set; }
        
        public WindowsOnlyRequiresMSBuildVersionFactAttribute(string version)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Skip = "This test requires Windows to run";
            }

            RequiresMSBuildVersionTheoryAttribute.CheckForRequiredMSBuildVersion(this, version);
        }
    }
}
