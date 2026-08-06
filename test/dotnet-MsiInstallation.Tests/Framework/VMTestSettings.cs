// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.MsiInstallerTests.Framework
{
    internal class VMTestSettings
    {
        public string VMName { get; set; }
        public string VMMachineName { get; set; }
        public string SdkInstallerVersion { get; set; }

        public bool ShouldTestStage2 { get; set; } = true;

        public string[] NuGetSourcesToAdd { get; set; }
    }
}
