// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.CodeAnalysis.Tools.Tests.Utilities
{
    public static class TestProjectsPathHelper
    {
        public static string GetProjectsDirectory()
        {
            var assetsDirectory = Path.Combine(SdkTestContext.Current.TestAssetsDirectory, "dotnet-format");
            if (Directory.Exists(assetsDirectory))
            {
                return assetsDirectory;
            }

            throw new ArgumentException("Can't find the project assets directory");
        }
    }
}
