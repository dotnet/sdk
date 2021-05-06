// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Microsoft.CodeAnalysis.Tools.Perf
{
    public static class MSBuildRegister
    {
        private static int s_registered;

        public static void RegisterInstance(string solutionDir)
        {
            if (Interlocked.Exchange(ref s_registered, 1) == 0)
            {
                var pathsToSearch = new List<string>();
                var sdkDirectory = new DirectoryInfo(Path.Combine(solutionDir, @".dotnet\sdk"));
                pathsToSearch.AddRange(sdkDirectory.EnumerateDirectories().Select(x => x.FullName));

                var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                if (programFiles is not null)
                {
                    var sdkPath = Path.Combine(programFiles, "dotnet", "sdk");
                    if (Directory.Exists(sdkPath))
                    {
                        sdkDirectory = new DirectoryInfo(Path.Combine(programFiles, @"dotnet\sdk"));
                        pathsToSearch.AddRange(sdkDirectory.EnumerateDirectories().Select(x => x.FullName));
                    }
                }

                var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                if (programFilesX86 is not null)
                {
                    var installs = Path.Combine(programFiles, "Microsoft Visual Studio", "2019");
                    if (Directory.Exists(installs))
                    {
                        pathsToSearch.AddRange(new DirectoryInfo(installs).EnumerateDirectories()
                            .Select(install => Path.Combine(install.FullName, "MSBuild", "Current", "Bin")));
                    }
                }

                Build.Locator.MSBuildLocator.RegisterMSBuildPath(pathsToSearch.ToArray());
            }
        }
    }
}
