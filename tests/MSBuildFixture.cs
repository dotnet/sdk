using System;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.Tools.MSBuild;

namespace Microsoft.CodeAnalysis.Tools.Tests
{
    public class MSBuildFixture : IDisposable
    {
        private static int _registered = 0;

        public void RegisterInstance()
        {
            if (Interlocked.Exchange(ref _registered, 1) == 0)
            {
                var msBuildInstance = Build.Locator.MSBuildLocator.QueryVisualStudioInstances().First();
                LooseVersionAssemblyLoader.Register(msBuildInstance.MSBuildPath);
                Build.Locator.MSBuildLocator.RegisterInstance(msBuildInstance);
            }
        }

        public void Dispose()
        {
        }
    }
}
