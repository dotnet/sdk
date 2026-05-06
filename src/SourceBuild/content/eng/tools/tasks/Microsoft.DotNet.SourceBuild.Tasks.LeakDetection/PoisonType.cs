using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.DotNet.SourceBuild.Tasks.LeakDetection
{
    [Flags]
    internal enum PoisonType
    {
        None = 0,
        Hash = 1,
        AssemblyAttribute = 2,
        NupkgFile = 4,
        SourceBuildReferenceAssembly = 8,
    }
}
