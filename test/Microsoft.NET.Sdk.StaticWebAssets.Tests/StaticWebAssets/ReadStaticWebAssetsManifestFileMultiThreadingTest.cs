// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Moq;

namespace Microsoft.AspNetCore.Razor.Tasks;

// This test mutates the process-wide current directory, so it must not run
// concurrently with other tests under MSTest's method-level parallelization.
[DoNotParallelize]
[TestClass]
public class ReadStaticWebAssetsManifestFileMultiThreadingTest
{
    [TestMethod]
    public void ReadsManifestRelativeToTaskEnvironmentProjectDirectory()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, nameof(ReadStaticWebAssetsManifestFileMultiThreadingTest), Guid.NewGuid().ToString("N"));
        var projectDir = Path.Combine(testRoot, "project");
        var spawnDir = Path.Combine(testRoot, "spawn");
        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(spawnDir);

        // Same relative file name in both directories, but with different content so the
        // source of the read is unambiguous. If the task resolved against the process CWD
        // (the decoy) instead of the TaskEnvironment.ProjectDirectory, it would read the
        // decoy manifest and surface "DecoyClassLib".
        File.WriteAllText(Path.Combine(projectDir, "manifest.json"), ManifestWithDiscoveryPatternSource("ProjectClassLib"));
        File.WriteAllText(Path.Combine(spawnDir, "manifest.json"), ManifestWithDiscoveryPatternSource("DecoyClassLib"));

        var currentDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(spawnDir);

            var buildEngine = new Mock<IBuildEngine>();
            var task = new ReadStaticWebAssetsManifestFile
            {
                BuildEngine = buildEngine.Object,
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir),
                ManifestPath = "manifest.json"
            };

            task.Execute().Should().BeTrue();

            task.DiscoveryPatterns.Length.Should().Be(1);
            task.DiscoveryPatterns[0].GetMetadata(nameof(StaticWebAssetsDiscoveryPattern.Source))
                .Should().Be("ProjectClassLib", "the manifest should be read from the project dir, not the process CWD");
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    private static string ManifestWithDiscoveryPatternSource(string source) => $@"{{
  ""Version"": 1,
  ""Hash"": ""__hash__"",
  ""Source"": ""{source}"",
  ""BasePath"": ""_content/{source}"",
  ""Mode"": ""Default"",
  ""ManifestType"": ""Build"",
  ""ReferencedProjectsConfiguration"": [ ],
  ""DiscoveryPatterns"": [
    {{
      ""Name"": ""{source}\\wwwroot"",
      ""Source"": ""{source}"",
      ""ContentRoot"": ""{source}/wwwroot"",
      ""BasePath"": ""_content/{source}"",
      ""Pattern"": ""**""
    }}
  ],
  ""Assets"": [],
  ""Endpoints"": []
}}";
}
