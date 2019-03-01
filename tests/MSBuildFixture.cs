using System;
using System.Linq;
using System.Threading;

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
                Build.Locator.MSBuildLocator.RegisterInstance(msBuildInstance);
            }
        }

        public void Dispose()
        {
        }
    }
}
