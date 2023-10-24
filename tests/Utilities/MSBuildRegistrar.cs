// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using System.Threading;

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

        public static string RegisterInstance()
        {
            if (Interlocked.Exchange(ref s_registered, 1) == 0)
            {
                var msBuildInstance = Build.Locator.MSBuildLocator.QueryVisualStudioInstances().First();
                s_msBuildPath = Path.EndsInDirectorySeparator(msBuildInstance.MSBuildPath)
                    ? msBuildInstance.MSBuildPath
                    : msBuildInstance.MSBuildPath + Path.DirectorySeparatorChar;
                Build.Locator.MSBuildLocator.RegisterMSBuildPath(s_msBuildPath);
            }

            return s_msBuildPath!;
        }
    }
}
