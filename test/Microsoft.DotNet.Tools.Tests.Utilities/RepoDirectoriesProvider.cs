// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public class RepoDirectoriesProvider
    {
        public readonly static string RepoRoot;
        public readonly static string TestWorkingFolder;
        public readonly static string DotnetUnderTest;
        public readonly static string DotnetRidUnderTest;
        public readonly static string Stage2AspNetCore;

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
                    // https://stackoverflow.com/a/60545278/294804
                    var assemblyConfigurationAttribute = typeof(RepoDirectoriesProvider).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>();
                    string configuration = assemblyConfigurationAttribute?.Configuration;
                    DotnetUnderTest = Path.Combine(RepoRoot, "artifacts", "bin", "redist-installer", configuration, "dotnet", "dotnet" + dotnetExtension);
                }
            }

            string AspNetCoreDir = Path.Combine(Path.GetDirectoryName(DotnetUnderTest), "shared", "Microsoft.AspNetCore.App");
            if (Directory.Exists(AspNetCoreDir))
            {
                Stage2AspNetCore = Directory.EnumerateDirectories(AspNetCoreDir).First();
            }

            //  TODO: Resolve dotnet folder even if DotnetUnderTest doesn't have full path
            var sdkFolders = Directory.GetDirectories(Path.Combine(Path.GetDirectoryName(DotnetUnderTest), "sdk"));
            sdkFolders.Length.Should().Be(1, "Only one SDK folder is expected in the layout");

            var sdkFolder = sdkFolders.Single();
            var versionFile = Path.Combine(sdkFolder, ".version");

            var lines = File.ReadAllLines(versionFile);
            DotnetRidUnderTest = lines[2].Trim();
        }
    }
}
