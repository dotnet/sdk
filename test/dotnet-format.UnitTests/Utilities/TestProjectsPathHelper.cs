// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.CodeAnalysis.Tools.Tests.Utilities
{
    public static class TestProjectsPathHelper
    {
        private static string s_projectsDirectory;

        public static string GetProjectsDirectory()
        {
            if (s_projectsDirectory == null)
            {
                var assetsDirectory = Path.Combine(SdkTestContext.Current.TestAssetsDirectory, "dotnet-format");
                if (Directory.Exists(assetsDirectory))
                {
                    s_projectsDirectory = assetsDirectory;
                    return assetsDirectory;
                }

                throw new ArgumentException("Can't find the project assets directory");
            }

            return s_projectsDirectory;
        }
    }
}
