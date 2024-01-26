// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.MsiInstallerTests
{
    //  Core actions:
    //  - Run command
    //  - Copy file to VM
    //  - Enumerate and read files / directories

    //  Run command
    //  - Install .NET SDK (From SdkTesting folder)
    //  - Uninstall .NET SDK
    //  - Workload install / uninstall
    //  - Apply rollback file
    //  - Get .NET version
    //  Copy file to VM
    //  - Deploy stage 2 SDK


    internal abstract class VMAction
    {
        public abstract SerializedVMAction Serialize();

        public abstract VMActionResult Run();

        

    }

    class SerializedVMAction
    {

    }

    class VMActionResult
    {

    }
}
