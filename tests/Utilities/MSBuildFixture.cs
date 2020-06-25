// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.Tools.MSBuild;
using Xunit.Abstractions;

namespace Microsoft.CodeAnalysis.Tools.Tests.Utilities
{
    /// <summary>
    /// This test fixture ensures that MSBuild is loaded.
    /// </summary>
    public class MSBuildFixture : IDisposable
    {
        private static int s_registered = 0;

        public void RegisterInstance(ITestOutputHelper output)
        {
            if (Interlocked.Exchange(ref s_registered, 1) == 0)
            {
                var msBuildInstance = Build.Locator.MSBuildLocator.QueryVisualStudioInstances().First();

                output.WriteLine(Resources.Using_msbuildexe_located_in_0, msBuildInstance.MSBuildPath);

                LooseVersionAssemblyLoader.Register(msBuildInstance.MSBuildPath);
                Build.Locator.MSBuildLocator.RegisterInstance(msBuildInstance);
            }
        }

        public void Dispose()
        {
        }
    }
}
