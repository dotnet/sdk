// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.Tools.MSBuild;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Microsoft.CodeAnalysis.Tools.Tests.Utilities
{
    /// <summary>
    /// Loads MSBuild assemblies.
    /// </summary>
    public sealed class MSBuildRegistrar
    {
        private static int s_registered;

        private static string? s_msBuildPath;


        public static string RegisterInstance(ILogger? logger = null)
        {
            if (Interlocked.Exchange(ref s_registered, 1) == 0)
            {
                var msBuildInstance = Build.Locator.MSBuildLocator.QueryVisualStudioInstances().First();

                s_msBuildPath = msBuildInstance.MSBuildPath;

                LooseVersionAssemblyLoader.Register(s_msBuildPath, logger);
                Build.Locator.MSBuildLocator.RegisterInstance(msBuildInstance);
            }

            return s_msBuildPath!;
        }
    }
}
