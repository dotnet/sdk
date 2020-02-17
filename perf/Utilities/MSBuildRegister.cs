// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.Tools.MSBuild;

namespace Microsoft.CodeAnalysis.Tools.Perf
{
    public static class MSBuildRegister
    {
        private static int _registered = 0;

        public static void RegisterInstance()
        {
            if (Interlocked.Exchange(ref _registered, 1) == 0)
            {
                var msBuildInstance = Build.Locator.MSBuildLocator.QueryVisualStudioInstances().First();
                LooseVersionAssemblyLoader.Register(msBuildInstance.MSBuildPath);
                Build.Locator.MSBuildLocator.RegisterInstance(msBuildInstance);
            }
        }
    }
}
