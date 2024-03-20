﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.Tools.Tests.Utilities
{
    public static class TestProjectsPathHelper
    {
        private static string s_projectsDirectory;

        public static string GetProjectsDirectory()
        {
            if (s_projectsDirectory == null)
            {
                var assetsDirectory = Path.Combine(TestContext.Current.TestAssetsDirectory, "dotnet-format.TestsProjects");
                if (Directory.Exists(assetsDirectory))
                {
                    s_projectsDirectory = assetsDirectory;
                    return assetsDirectory;
                }

                throw new ArgumentException("Can't find the project asserts directory");
            }

            return s_projectsDirectory;
        }
    }
}
