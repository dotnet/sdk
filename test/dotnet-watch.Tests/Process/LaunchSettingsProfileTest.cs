// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

extern alias MSTestFramework;

#nullable disable

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class LaunchSettingsProfileTest
{
    public TestContext TestContext { get; set; } = null!;
    private DualOutputHelper _output;
    private DualOutputHelper Output => _output ??= new(new MSTestFramework::Microsoft.NET.TestFramework.TestContextOutputHelper(TestContext));
    private ILogger _logger;
    private ILogger Logger => _logger ??= new TestLogger(Output);
    private TestAssetsManager _testAssets;
    private TestAssetsManager TestAssets => _testAssets ??= new TestAssetsManager(Output);

    [TestMethod]
    public void LoadsLaunchProfiles()
    {
        var project = TestAssets.CreateTestProject(new TestProject("Project1")
        {
            TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
        });

        WriteFile(project, Path.Combine("Properties", "launchSettings.json"),
        """
        {
          "profiles": {
            "http": {
              "applicationUrl": "http://localhost:5000",
              "commandName": "Project",
              "environmentVariables": {
                "ASPNETCORE_ENVIRONMENT": "Development"
              }
            },
            "https": {
              "applicationUrl": "https://localhost:5001",
              "commandName": "Project",
              "environmentVariables": {
                "ASPNETCORE_ENVIRONMENT": "Development"
              }
            }, // This comment and trailing comma shouldn't cause any issues
          }
        }
        """);

        var projectPath = new ProjectRepresentation(
            projectPath: Path.Combine(project.TestRoot, "Project1", "Project1.csproj"),
            entryPointFilePath: null);

        var expected = LaunchSettingsProfile.ReadLaunchProfile(projectPath, launchProfileName: "http", Logger);
        Assert.IsNotNull(expected);
        Assert.AreEqual("http://localhost:5000", expected.ApplicationUrl);

        expected = LaunchSettingsProfile.ReadLaunchProfile(projectPath, "https", Logger);
        Assert.IsNotNull(expected);
        Assert.AreEqual("https://localhost:5001", expected.ApplicationUrl);

        expected = LaunchSettingsProfile.ReadLaunchProfile(projectPath, "notfound", Logger);
        Assert.IsNotNull(expected);
    }

    [TestMethod]
    public void DefaultLaunchProfileWithoutProjectCommand()
    {
        var project = TestAssets.CreateTestProject(new TestProject("Project1")
        {
            TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
        });

        WriteFile(project, Path.Combine("Properties", "launchSettings.json"),
        """
        {
          "profiles": {
            "profile": {
              "applicationUrl": "http://localhost:5000"
            }
          }
        }
        """);

        var projectPath = new ProjectRepresentation(
            projectPath: Path.Combine(project.Path, "Project1", "Project1.csproj"),
            entryPointFilePath: null);

        var expected = LaunchSettingsProfile.ReadLaunchProfile(projectPath, launchProfileName: null, Logger);
        Assert.IsNull(expected);
    }

    private static string WriteFile(TestAsset testAsset, string name, string contents = "")
    {
        var path = Path.Combine(GetTestProjectDirectory(testAsset), name);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, contents);

        return path;
    }

    private static string GetTestProjectDirectory(TestAsset testAsset)
        => Path.Combine(testAsset.Path, testAsset.TestProject.Name);
}
