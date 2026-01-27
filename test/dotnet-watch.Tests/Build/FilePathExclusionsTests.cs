// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Build.Execution;
using Microsoft.Build.Graph;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch.UnitTests
{
    public class FilePathExclusionsTests(ITestOutputHelper output)
    {
        private readonly TestAssetsManager _testAssets = new(output);

        [Theory]
        [InlineData("bin/**")]
        [InlineData("bin/**/*")]
        [InlineData("obj/**")]
        [InlineData("**/node_modules/**")]
        [InlineData("**/**/dist/**")]
        public void ExcludesDirectoriesMatchingGlobPatterns(string exclusionPattern)
        {
            // Create a simple test project with default item excludes
            var project = new TestProject("TestProject")
            {
                IsExe = true,
                SourceFiles =
                {
                    {"Program.cs", "return 0;"}
                }
            };

            var testAsset = _testAssets.CreateTestProject(project)
                .WithProjectChanges(d =>
                {
                    // Add custom exclusion pattern to DefaultItemExcludes
                    d.Root!.Add(XElement.Parse($"""
                        <PropertyGroup>
                          <DefaultItemExcludes>$(DefaultItemExcludes);{exclusionPattern}</DefaultItemExcludes>
                        </PropertyGroup>
                        """));
                });

            var projectPath = Path.Combine(testAsset.Path, "TestProject", "TestProject.csproj");
            var projectGraph = new ProjectGraph(projectPath);
            
            var exclusions = FilePathExclusions.Create(projectGraph);
            
            // Verify that the excluded directory was identified
            Assert.NotEmpty(exclusions.ExcludedDirectories);
        }

        [Fact]
        public void SkipsWatchingExcludedDirectories()
        {
            var logger = new TestLogger(output);
            var watcher = new TestFileWatcher(logger);
            
            var root = TestContext.Current.TestExecutionDirectory;
            var dirToWatch = Path.Combine(root, "watched");
            var dirToExclude = Path.Combine(root, "excluded");
            
            var fileInWatched = Path.Combine(dirToWatch, "file.cs");
            var fileInExcluded = Path.Combine(dirToExclude, "file.cs");
            
            var excludedDirs = new HashSet<string>(PathUtilities.OSSpecificPathComparer)
            {
                dirToExclude
            };
            
            // Watch both directories, but one is excluded
            watcher.WatchContainingDirectories([fileInWatched, fileInExcluded], includeSubdirectories: true, excludedDirs);
            
            // Verify only the non-excluded directory is watched
            Assert.Single(watcher.DirectoryTreeWatchers);
            Assert.Contains(PathUtilities.EnsureTrailingSlash(dirToWatch), watcher.DirectoryTreeWatchers.Keys);
            Assert.DoesNotContain(PathUtilities.EnsureTrailingSlash(dirToExclude), watcher.DirectoryTreeWatchers.Keys);
        }

        private sealed class TestFileWatcher(ILogger logger)
            : FileWatcher(logger, TestOptions.GetEnvironmentOptions())
        {
            public IReadOnlyDictionary<string, DirectoryWatcher> DirectoryTreeWatchers => _directoryTreeWatchers;
            public IReadOnlyDictionary<string, DirectoryWatcher> DirectoryWatchers => _directoryWatchers;

            protected override DirectoryWatcher CreateDirectoryWatcher(string directory, ImmutableHashSet<string> fileNames, bool includeSubdirectories)
                => new TestDirectoryWatcher(directory, fileNames, includeSubdirectories);
        }

        private sealed class TestDirectoryWatcher(string watchedDirectory, ImmutableHashSet<string> watchedFileNames, bool includeSubdirectories)
            : DirectoryWatcher(watchedDirectory, watchedFileNames, includeSubdirectories)
        {
            public override bool EnableRaisingEvents { get; set; }
            public override void Dispose() { }
        }
    }
}
