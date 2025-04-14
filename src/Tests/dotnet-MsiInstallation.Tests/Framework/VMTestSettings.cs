// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.MsiInstallerTests.Framework
{
    internal class VMTestSettings
    {
        public string VMName { get; set; }
        public string VMMachineName { get; set; }
        public string SdkInstallerVersion { get; set; }

        public bool ShouldTestStage2 { get; set; } = true;


    }
}
