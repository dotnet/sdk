// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Microsoft.DotNet.PlatformAbstractions;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public class RepoDirectoriesProvider
    {
        public readonly static string RepoRoot;
   
        public readonly static string TestWorkingFolder;
        public readonly static string DotnetUnderTest;

        static RepoDirectoriesProvider()
        {

#if NET451
            string directory = AppDomain.CurrentDomain.BaseDirectory;
#else
            string directory = AppContext.BaseDirectory;
#endif

            while (directory != null)
            {
                var gitDirOrFile = Path.Combine(directory, ".git");
                if (Directory.Exists(gitDirOrFile) || File.Exists(gitDirOrFile))
                {
                    break;
                }
                directory = Directory.GetParent(directory)?.FullName;
            }

            RepoRoot = directory;

            TestWorkingFolder = Environment.GetEnvironmentVariable("CORESDK_TEST_FOLDER");
            if (string.IsNullOrEmpty(TestWorkingFolder))
            {
                TestWorkingFolder = Path.Combine(AppContext.BaseDirectory, "Tests");
            }

            DotnetUnderTest = Environment.GetEnvironmentVariable("DOTNET_UNDER_TEST");
            string dotnetExtension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";
            if (string.IsNullOrEmpty(DotnetUnderTest))
            {
                if (RepoRoot == null)
                {
                    DotnetUnderTest = "dotnet" + dotnetExtension;
                }
                else
                {
                    string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent.Name;
                    DotnetUnderTest = Path.Combine(RepoRoot, "artifacts", "bin", "redist", configuration, "dotnet", "dotnet" + dotnetExtension);
                }
            }
        }

    }
}
