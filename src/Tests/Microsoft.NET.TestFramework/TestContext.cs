﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework.Commands;
using System.Reflection;
using System.Globalization;

namespace Microsoft.NET.TestFramework
{
    public class TestContext
    {
        //  Generally the folder the test DLL is in
        public string TestExecutionDirectory { get; set; }

        public string TestAssetsDirectory { get; set; }

        public string TestPackages { get; set; }

        //  For tests which want the global packages folder isolated in the repo, but
        //  can share it with other tests
        public string TestGlobalPackagesFolder { get; set; }

        public string NuGetCachePath { get; set; }

        public string NuGetFallbackFolder { get; set; }

        public string NuGetExePath { get; set; }

        public string SdkVersion { get; set; }

        public ToolsetInfo ToolsetUnderTest { get; set; }

        private static TestContext _current;

        public static TestContext Current
        {
            get
            {
                if (_current == null)
                {
                    //  Initialize test context in cases where it hasn't been initialized via the entry point
                    //  (ie when using test explorer or another runner)
                    Initialize(TestCommandLine.Parse(Array.Empty<string>()));
                }
                return _current;
            }
            set
            {
                _current = value;
            }
        }

        public const string LatestRuntimePatchForNetCoreApp2_0 = "2.0.9";

        public void AddTestEnvironmentVariables(SdkCommandSpec command)
        {
            command.Environment["DOTNET_MULTILEVEL_LOOKUP"] = "0";

            //  Set NUGET_PACKAGES environment variable to match value from build.ps1
            command.Environment["NUGET_PACKAGES"] = NuGetCachePath;

            command.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";

            command.Environment["GenerateResourceMSBuildArchitecture"] = "CurrentArchitecture";
            command.Environment["GenerateResourceMSBuildRuntime"] = "CurrentRuntime";

            //  Prevent test MSBuild nodes from persisting
            command.Environment["MSBUILDDISABLENODEREUSE"] = "1";

            ToolsetUnderTest.AddTestEnvironmentVariables(command);
        }


        public static void Initialize(TestCommandLine commandLine)
        {
            //  Show verbose debugging output for tests
            CommandContext.SetVerbose(true);
            Reporter.Reset();

            Environment.SetEnvironmentVariable("DOTNET_MULTILEVEL_LOOKUP", "0");

            //  Reset this environment variable so that if the dotnet under test is different than the
            //  one running the tests, it won't interfere
            Environment.SetEnvironmentVariable("MSBuildSdksPath", null);

            TestContext testContext = new TestContext();
            
            bool runAsTool = false;
            if (Directory.Exists(Path.Combine(AppContext.BaseDirectory, "Assets")))
            {
                runAsTool = true;
                testContext.TestAssetsDirectory = Path.Combine(AppContext.BaseDirectory, "Assets");
            }
            else if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_SDK_TEST_AS_TOOL")))
            {
                //  Pretend to run as a tool, but use the test assets found in the repo
                //  This allows testing most of the "tests as global tool" behavior by setting an environment
                //  variable instead of packing the test, and installing it as a global tool.
                runAsTool = true;
                
                testContext.TestAssetsDirectory = FindFolderInTree(Path.Combine("src", "Assets"), AppContext.BaseDirectory);
            }

            string repoRoot = null;
#if DEBUG
            string repoConfiguration = "Debug";
#else
            string repoConfiguration = "Release";
#endif

            if (commandLine.SDKRepoPath != null)
            {
                repoRoot = commandLine.SDKRepoPath;
            }
            else if (!commandLine.NoRepoInference && !runAsTool)
            {
                repoRoot = GetRepoRoot();

                //if (repoRoot != null)
                //{
                //    // assumes tests are always executed from the "artifacts/bin/Tests/$MSBuildProjectFile/$Configuration" directory
                //    repoConfiguration = new DirectoryInfo(AppContext.BaseDirectory).Name;
                //}
            }

            if (!string.IsNullOrEmpty(commandLine.TestExecutionDirectory))
            {
                testContext.TestExecutionDirectory = commandLine.TestExecutionDirectory;
            }
            else if (runAsTool)
            {
                testContext.TestExecutionDirectory = Path.Combine(Path.GetTempPath(), "dotnetSdkTests", Path.GetRandomFileName());
            }
            else
            {
                testContext.TestExecutionDirectory = (Path.Combine(FindFolderInTree("artifacts", AppContext.BaseDirectory), "tmp", repoConfiguration));

                testContext.TestAssetsDirectory = FindFolderInTree(Path.Combine("src", "Assets"), AppContext.BaseDirectory);
            }

            Directory.CreateDirectory(testContext.TestExecutionDirectory);

            string artifactsDir = Environment.GetEnvironmentVariable("DOTNET_SDK_ARTIFACTS_DIR");
            if (string.IsNullOrEmpty(artifactsDir) && !string.IsNullOrEmpty(repoRoot))
            {
                artifactsDir = Path.Combine(repoRoot, "artifacts");
            }

            if (!string.IsNullOrEmpty(artifactsDir))
            {
                testContext.TestGlobalPackagesFolder = Path.Combine(artifactsDir, ".nuget", "packages");
            }
            else
            {
                testContext.TestGlobalPackagesFolder = Path.Combine(testContext.TestExecutionDirectory, ".nuget", "packages");
            }

            if (repoRoot != null)
            {
                testContext.NuGetFallbackFolder = Path.Combine(artifactsDir, ".nuget", "NuGetFallbackFolder");
                testContext.NuGetExePath = Path.Combine(artifactsDir, ".nuget", $"nuget{Constants.ExeSuffix}");
                testContext.NuGetCachePath = Path.Combine(artifactsDir, ".nuget", "packages");

                testContext.TestPackages = Path.Combine(artifactsDir, "tmp", repoConfiguration, "testpackages");
            }
            else
            {
                var nugetFolder = FindFolderInTree(".nuget", AppContext.BaseDirectory, false)
                    ?? Path.Combine(testContext.TestExecutionDirectory, ".nuget");
                

                testContext.NuGetFallbackFolder = Path.Combine(nugetFolder, "NuGetFallbackFolder");
                testContext.NuGetExePath = Path.Combine(nugetFolder, $"nuget{Constants.ExeSuffix}");
                testContext.NuGetCachePath = Path.Combine(nugetFolder, "packages");
            }

            if (commandLine.SdkVersion != null)
            {
                testContext.SdkVersion = commandLine.SdkVersion;
            }

            testContext.ToolsetUnderTest = ToolsetInfo.Create(repoRoot, artifactsDir, repoConfiguration, commandLine);

            //  Important to set this before below code which ends up calling through TestContext.Current, which would
            //  result in infinite recursion / stack overflow if TestContext.Current wasn't set
            TestContext.Current = testContext;

            //  Set up test hooks for in-process tests
            Environment.SetEnvironmentVariable(
                DotNet.Cli.Utils.Constants.MSBUILD_EXE_PATH,
                Path.Combine(testContext.ToolsetUnderTest.SdkFolderUnderTest, "MSBuild.dll"));

            Environment.SetEnvironmentVariable(
                "MSBuildSDKsPath",
                Path.Combine(testContext.ToolsetUnderTest.SdksPath));

            DotNet.Cli.Utils.MSBuildForwardingAppWithoutLogging.MSBuildExtensionsPathTestHook =
                testContext.ToolsetUnderTest.SdkFolderUnderTest;
        }

        public static string GetRepoRoot()
        {
            string directory = AppContext.BaseDirectory;

            while (!Directory.Exists(Path.Combine(directory, ".git")) && directory != null)
            {
                directory = Directory.GetParent(directory)?.FullName;
            }

            if (directory == null)
            {
                return null;
            }
            return directory;
        }
        private static string FindOrCreateFolderInTree(string relativePath, string startPath)
        {
            string ret = FindFolderInTree(relativePath, startPath, throwIfNotFound: false);
            if (ret != null)
            {
                return ret;
            }
            ret = Path.Combine(startPath, relativePath);
            Directory.CreateDirectory(ret);
            return ret;
        }
        private static string FindFolderInTree(string relativePath, string startPath, bool throwIfNotFound = true)
        {
            string currentPath = startPath;
            while (true)
            {
                string path = Path.Combine(currentPath, relativePath);
                if (Directory.Exists(path))
                {
                    return path;
                }
                var parent = Directory.GetParent(currentPath);
                if (parent == null)
                {
                    if (throwIfNotFound)
                    {
                        throw new FileNotFoundException($"Could not find folder '{relativePath}' in '{startPath}' or any of its ancestors");
                    }
                    else
                    {
                        return null;
                    }
                }
                currentPath = parent.FullName;
            }
        }

        public void WriteGlobalJson(string path)
        {
            WriteGlobalJson(path, this.SdkVersion);
        }

        public static void WriteGlobalJson(string path, string sdkVersion)
        {
            if (!string.IsNullOrEmpty(sdkVersion))
            {
                string globalJsonPath = System.IO.Path.Combine(path, "global.json");
                File.WriteAllText(globalJsonPath, @"{
  ""sdk"": {
    ""version"": """ + sdkVersion + @"""
  }
}");
            }
        }

        public static bool IsLocalized()
        {
            for (var culture = CultureInfo.CurrentUICulture; !culture.Equals(CultureInfo.InvariantCulture); culture = culture.Parent)
            {
                if (culture.Name == "en")
                {
                    return false;
                }
            }

            return true;
        }
    }
}
