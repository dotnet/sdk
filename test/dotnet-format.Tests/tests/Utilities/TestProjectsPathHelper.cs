// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.IO;
using System.Reflection;

namespace Microsoft.CodeAnalysis.Tools.Tests.Utilities
{
    public static class TestProjectsPathHelper
    {
        private static string s_projectsDirectory;

        public static string GetProjectsDirectory()
        {
            if (s_projectsDirectory == null)
            {
                s_projectsDirectory = Path.Combine(TestContext.Current.TestAssetsDirectory, "dotnet-format.TestsProjects");
            }

            return s_projectsDirectory;
        }
    }
}
