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
                // walk from /format/artifacts/bin/dotnet-format.UnitTests/Debug/netcoreapp2.1/dotnet-format.UnitTests.dll
                // up to the repo root then down to the test projects folder.
                var unitTestAssemblyPath = Assembly.GetExecutingAssembly().Location;
                var repoRootPath = Directory.GetParent(unitTestAssemblyPath).Parent.Parent.Parent.Parent.Parent.FullName;
                s_projectsDirectory = Path.Combine(repoRootPath, "tests", "projects");
            }

            return s_projectsDirectory;
        }
    }
}
