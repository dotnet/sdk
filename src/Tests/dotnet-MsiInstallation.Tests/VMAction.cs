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

    class SerializedRunCommand
    {
        public List<string> Arguments { get; set; } = new List<string>();

        public override bool Equals(object obj)
        {
            if (obj is SerializedRunCommand other)
            {
                return Arguments.SequenceEqual(other.Arguments);
            }
            return false;
        }

        public override int GetHashCode()
        {
            var hashcode = new HashCode();
            hashcode.Add(Arguments.Count);
            foreach (var arg in Arguments)
            {
                hashcode.Add(arg.GetHashCode());
            }
            return hashcode.ToHashCode();
        }
    }

    class VMActionResult
    {

    }
}
