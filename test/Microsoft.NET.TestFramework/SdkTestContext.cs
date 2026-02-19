// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.TestFramework
{
    public class SdkTestContext
    {
        //  Generally the folder the test DLL is in
        private string? _testExecutionDirectory;

        public string TestExecutionDirectory
        {
            get
            {
                if (_testExecutionDirectory == null)
                {
                    throw new InvalidOperationException("TestExecutionDirectory should never be null.");
                }
                return _testExecutionDirectory;
            }
            set
            {
                _testExecutionDirectory = value;
            }
        }

        private string? _testAssetsDirectory;

        public string TestAssetsDirectory
        {
            get
            {
                if (_testAssetsDirectory == null)
                {
                    throw new InvalidOperationException("TestAssetsDirectory should never be null.");
                }
                return _testAssetsDirectory;
            }
            set
            {
                _testAssetsDirectory = value;
            }
        }

        public string? TestPackages { get; set; }

        //  For tests which want the global packages folder isolated in the repo, but
        //  can share it with other tests
        public string? TestGlobalPackagesFolder { get; set; }

        public string? NuGetCachePath { get; set; }

        public string? NuGetFallbackFolder { get; set; }

        public string? NuGetExePath { get; set; }

        public string? ShippingPackagesDirectory { get; set; }

        /// <summary>
        /// Finds a single SDK acquisition artifact (tar.gz, pkg, deb, rpm) matching the specified pattern
        /// in the <see cref="ShippingPackagesDirectory"/>.
        /// </summary>
        /// <param name="filePattern">The file pattern to search for (e.g., "dotnet-sdk-*.tar.gz").</param>
        /// <returns>The full path to the matching artifact.</returns>
        public static string FindSdkAcquisitionArtifact(string filePattern)
        {
            if (!FindOptionalSdkAcquisitionArtifact(filePattern, [], out string? artifactPath))
            {
                throw new InvalidOperationException(
                    $"No files matching '{filePattern}' found in '{Current.ShippingPackagesDirectory}'.");
            }
            return artifactPath!;
        }

        /// <summary>
        /// Finds an optional SDK acquisition artifact matching the pattern. Returns false if no artifacts
        /// exist (e.g., platform doesn't produce this artifact type). Throws if multiple artifacts match
        /// after filtering (unexpected configuration error).
        /// </summary>
        /// <param name="filePattern">The file pattern to search for (e.g., "dotnet-sdk-*.pkg").</param>
        /// <param name="excludeSubstrings">Substrings to exclude from filenames (e.g., "-internal", "-newkey").</param>
        /// <param name="artifactPath">The full path to the matching artifact, or null if not found.</param>
        /// <returns>True if exactly one matching artifact was found; false if no artifacts exist.</returns>
        public static bool FindOptionalSdkAcquisitionArtifact(string filePattern, string[] excludeSubstrings, out string? artifactPath)
        {
            string? shippingDir = Current.ShippingPackagesDirectory;
            if (string.IsNullOrEmpty(shippingDir) || !Directory.Exists(shippingDir))
            {
                throw new InvalidOperationException($"ShippingPackagesDirectory '{shippingDir}' does not exist.");
            }

            var files = Directory.GetFiles(shippingDir, filePattern);
            if (files.Length == 0)
            {
                artifactPath = null;
                return false;
            }

            var filteredFiles = files.Where(f =>
            {
                var fileName = Path.GetFileNameWithoutExtension(f);
                return !excludeSubstrings.Any(suffix => fileName.Contains(suffix));
            }).ToArray();

            if (filteredFiles.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Expected 1 {filePattern} file after filtering. Found: [{string.Join(", ", files.Select(Path.GetFileName))}], filtered: [{string.Join(", ", filteredFiles.Select(Path.GetFileName))}]");
            }

            artifactPath = filteredFiles[0];
            return true;
        }

        private ToolsetInfo? _toolsetUnderTest;

        public ToolsetInfo ToolsetUnderTest
        {
            get
            {
                if (_toolsetUnderTest == null)
                {
                    throw new InvalidOperationException("ToolsetUnderTest should never be null.");
                }
                return _toolsetUnderTest;
            }
            set
            {
                _toolsetUnderTest = value;
            }
        }

        private static SdkTestContext? _current;

        public static SdkTestContext Current
        {
            get
            {
                if (_current == null)
                {
                    Initialize();
                }
                return _current ?? throw new InvalidOperationException("SdkTestContext.Current should never be null.");
            }
            set
            {
                _current = value;
            }
        }

        public const string LatestRuntimePatchForNetCoreApp2_0 = "2.0.9";

        public static string GetRuntimeGraphFilePath()
        {
            string dotnetRoot = SdkTestContext.Current.ToolsetUnderTest.DotNetRoot;

            DirectoryInfo sdksDir = new(Path.Combine(dotnetRoot, "sdk"));

            var lastWrittenSdk = sdksDir.EnumerateDirectories().OrderByDescending(di => di.LastWriteTime).First();

            return lastWrittenSdk.GetFiles("RuntimeIdentifierGraph.json").Single().FullName;
        }

        public void AddTestEnvironmentVariables(IDictionary<string, string> environment)
        {
            //  Set NUGET_PACKAGES environment variable to match value from build.ps1
            if(NuGetCachePath is not null)
            {
                environment["NUGET_PACKAGES"] = NuGetCachePath;
            }

            environment["GenerateResourceMSBuildArchitecture"] = "CurrentArchitecture";
            environment["GenerateResourceMSBuildRuntime"] = "CurrentRuntime";

            //  Prevent test MSBuild nodes from persisting
            environment["MSBUILDDISABLENODEREUSE"] = "1";

            ToolsetUnderTest.AddTestEnvironmentVariables(environment);
        }


        public static void Initialize()
        {
            //  Show verbose debugging output for tests
            CommandLoggingContext.SetVerbose(true);
            Reporter.Reset();

            //  Reset this environment variable so that if the dotnet under test is different than the
            //  one running the tests, it won't interfere
            Environment.SetEnvironmentVariable("MSBuildSdksPath", null);

            SdkTestContext testContext = new();

            string basePath = Path.Combine(AppContext.BaseDirectory, "TestAssets");
            string? envTestAssetsDir = Environment.GetEnvironmentVariable("DOTNET_SDK_TEST_ASSETS_DIRECTORY");
            testContext.TestAssetsDirectory =
                (Directory.Exists(basePath) ? basePath : null)
                ?? (!string.IsNullOrEmpty(envTestAssetsDir) ? envTestAssetsDir : null)
                ?? FindFolderInTree(Path.Combine("test", "TestAssets"), AppContext.BaseDirectory)!;

#if DEBUG
            string repoConfiguration = "Debug";
#else
            string repoConfiguration = "Release";
#endif

            string? repoRoot = GetRepoRoot();

            string? envTestExecDir = Environment.GetEnvironmentVariable("DOTNET_SDK_TEST_EXECUTION_DIRECTORY");
            testContext.TestExecutionDirectory =
                (!string.IsNullOrEmpty(envTestExecDir) ? envTestExecDir : null)
                ?? Path.Combine(FindFolderInTree("artifacts", AppContext.BaseDirectory)!, "tmp", repoConfiguration, "testing");

            Directory.CreateDirectory(testContext.TestExecutionDirectory);

            string? artifactsDir = Environment.GetEnvironmentVariable("DOTNET_SDK_ARTIFACTS_DIR");
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

            if (repoRoot != null && artifactsDir is not null)
            {
                testContext.NuGetFallbackFolder = Path.Combine(artifactsDir, ".nuget", "NuGetFallbackFolder");
                testContext.NuGetExePath = Path.Combine(artifactsDir, ".nuget", $"nuget{Constants.ExeSuffix}");
                testContext.NuGetCachePath = Path.Combine(artifactsDir, ".nuget", "packages");

                testContext.TestPackages = Path.Combine(artifactsDir, "tmp", repoConfiguration, "testing", "testpackages");
                testContext.ShippingPackagesDirectory = Path.Combine(artifactsDir, "packages", repoConfiguration, "Shipping");
            }
            else
            {
                var nugetFolder = FindFolderInTree(".nuget", AppContext.BaseDirectory, false)
                    ?? Path.Combine(testContext.TestExecutionDirectory, ".nuget");

                testContext.NuGetFallbackFolder = Path.Combine(nugetFolder, "NuGetFallbackFolder");
                testContext.NuGetExePath = Path.Combine(nugetFolder, $"nuget{Constants.ExeSuffix}");
                testContext.NuGetCachePath = Path.Combine(nugetFolder, "packages");

                var testPackages = Path.Combine(testContext.TestExecutionDirectory, "Testpackages");
                if (Directory.Exists(testPackages))
                {
                    testContext.TestPackages = testPackages;
                }
            }

            if (testContext.ShippingPackagesDirectory is null)
            {
                string? dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
                if (!string.IsNullOrEmpty(dotnetRoot))
                {
                    testContext.ShippingPackagesDirectory = Path.Combine(dotnetRoot, ".nuget");
                }
            }

            testContext.ToolsetUnderTest = ToolsetInfo.Create(repoRoot, artifactsDir, repoConfiguration);

            //  Important to set this before below code which ends up calling through SdkTestContext.Current, which would
            //  result in infinite recursion / stack overflow if SdkTestContext.Current wasn't set
            Current = testContext;

            //  Set up test hooks for in-process tests
            Environment.SetEnvironmentVariable(
                Constants.MSBUILD_EXE_PATH,
                Path.Combine(testContext.ToolsetUnderTest.SdkFolderUnderTest, "MSBuild.dll"));

            Environment.SetEnvironmentVariable(
                "MSBuildSDKsPath",
                Path.Combine(testContext.ToolsetUnderTest.SdksPath));

#if NETCOREAPP
            MSBuildForwardingAppWithoutLogging.MSBuildExtensionsPathTestHook =
                testContext.ToolsetUnderTest.SdkFolderUnderTest;
#endif
        }

        public static string? GetRepoRoot()
        {
            string? directory = AppContext.BaseDirectory;

            while (directory is not null)
            {
                var gitPath = Path.Combine(directory, ".git");
                if (Directory.Exists(gitPath) || File.Exists(gitPath))
                {
                    // Found the repo root, which should either have a .git folder or, if the repo
                    // is part of a Git worktree, a .git file.
                    return directory;
                }

                directory = Directory.GetParent(directory)?.FullName;
            }

            return null;
        }
        private static string FindOrCreateFolderInTree(string relativePath, string startPath)
        {
            string? ret = FindFolderInTree(relativePath, startPath, throwIfNotFound: false);
            if (ret != null)
            {
                return ret;
            }
            ret = Path.Combine(startPath, relativePath);
            Directory.CreateDirectory(ret);
            return ret;
        }
        private static string? FindFolderInTree(string relativePath, string startPath, bool throwIfNotFound = true)
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
